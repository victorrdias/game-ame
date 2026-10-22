using System;
using System.Collections.Generic;
using Godot;

namespace Vilanova;

public partial class NpcController : CharacterBody3D
{
	public const string GroupName = "npc_resident";

	public NpcResident Resident { get; private set; }
	public bool BlocksPlacement =>
		_initialized &&
		Resident.State != NpcRoutineState.InsideBuilding &&
		Resident.State != NpcRoutineState.LeavingBuilding &&
		!_activityCollisionDisabled;

	private NpcPopulationManager _population = null!;
	private NpcRoutineConfig _config = null!;
	private CollisionShape3D _collisionShape = null!;
	private Node3D _visualRoot = null!;
	private readonly List<Vector2I> _cellPath = new();
	private readonly List<Vector3> _waypoints = new();
	private readonly List<Vector3> _pending = new();
	private readonly List<BuildingApproach> _visitOptions = new();
	private readonly List<NpcInteractionPoint> _activityOptions = new();
	private Random _rng = null!;
	private NpcActivityVisual _activityVisual = null!;
	private NpcInteractionPoint _activityPoint;
	private ActivityPerformPhase _activityPhase = ActivityPerformPhase.None;
	private float _activityCooldownRemaining;
	private float _alignRemaining;
	private float _activityPerformRemaining;
	private float _phaseElapsed;
	private Vector3 _activityHoldPosition;
	private bool _activityCollisionDisabled;
	private bool _lockActivityBody;
	private bool _standUpFinished;
	private Vector2I _goalCell;
	private string _targetBuildingId;
	private float _speed = 2.0f;
	private float _visualYaw;
	private float _waitRemaining;
	private float _insideRemaining;
	private float _leaveWait;
	private float _stuckTime;
	private int _stuckRepaths;
	private int _waypointIndex;
	private Vector3 _stuckAnchor;
	private Vector3 _avoidDirection;
	private ulong _avoidUntilMs;
	private int _avoidCount;
	private bool _hasPath;
	private bool _preferWander;
	private bool _initialized;

	public override void _Ready()
	{
		_collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
		_visualRoot = GetNode<Node3D>("VisualRoot");
		_visualRoot.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
		MotionMode = MotionModeEnum.Grounded;
		FloorSnapLength = 0.45f;
		FloorMaxAngle = Mathf.DegToRad(50.0f);
		CollisionLayer = CollisionLayers.Npc;
		CollisionMask = CollisionLayers.NpcMask;
		AddToGroup(GroupName);
	}

	public void Initialize(NpcResident resident, NpcPopulationManager population, NpcRoutineConfig config)
	{
		Resident = resident;
		_population = population;
		_config = config;
		_rng = new Random(HashCode.Combine(resident.Id));
		_speed = config.MoveSpeed * (0.88f + (float)_rng.NextDouble() * 0.24f);
		_visualYaw = (float)_rng.NextDouble() * Mathf.Tau;
		_visualRoot.Basis = new Basis(Vector3.Up, _visualYaw);
		_stuckAnchor = GlobalPosition;
		_activityVisual = _visualRoot.GetNodeOrNull<NpcActivityVisual>("Activity");
		SetPresent(true);
		EnterIdle();
		_initialized = true;
	}

	public Aabb GetOccupancyBounds()
	{
		var shape = _collisionShape?.Shape as CapsuleShape3D;
		var radius = shape?.Radius ?? 0.28f;
		var height = shape?.Height ?? 0.9f;
		var center = GlobalPosition + new Vector3(0, height * 0.5f + radius, 0);
		var size = new Vector3(radius * 2.2f, height + radius * 2.0f, radius * 2.2f);
		return new Aabb(center - size * 0.5f, size);
	}

	public void OnNavigationChanged()
	{
		if (!_initialized)
		{
			return;
		}

		if (Resident.State != NpcRoutineState.Wandering &&
		    Resident.State != NpcRoutineState.GoingToBuilding &&
		    Resident.State != NpcRoutineState.GoingToActivity)
		{
			return;
		}

		if (PathStillValid())
		{
			return;
		}

		if (!TryRepath())
		{
			EnterIdle();
		}
	}

	public void HandleBuildingRemoved(string instanceId, Vector3 origin)
	{
		if (!_initialized || string.IsNullOrEmpty(instanceId))
		{
			return;
		}

		if (Resident.HomeBuildingId == instanceId)
		{
			Resident.HomeBuildingId = null;
		}

		var involved = _targetBuildingId == instanceId || Resident.CurrentBuildingId == instanceId;
		if (!involved)
		{
			OnNavigationChanged();
			return;
		}

		_targetBuildingId = null;
		if (Resident.State == NpcRoutineState.InsideBuilding || Resident.State == NpcRoutineState.LeavingBuilding)
		{
			if (!_population.TryFindStandPoint(origin, this, out var spot))
			{
				GD.PushWarning($"{Resident.DisplayName} não achou vaga ao sair de um edifício demolido.");
				spot = _population.Walkability.StandPoint(origin);
			}

			AppearAt(spot);
			return;
		}

		if (Resident.State is NpcRoutineState.GoingToActivity or NpcRoutineState.AligningToActivity or NpcRoutineState.PerformingActivity)
		{
			CancelActivity(false);
		}

		EnterIdle();
	}

	public void HandleActivitySourceRemoved(string instanceId, Vector3 origin)
	{
		if (!_initialized || string.IsNullOrEmpty(instanceId) || _activityPoint == null)
		{
			return;
		}

		if (_activityPoint.SourceInstanceId != instanceId)
		{
			return;
		}

		CancelActivity(true);
		if (!_population.TryFindStandPoint(origin, this, out var spot))
		{
			spot = _population.Walkability.StandPoint(origin);
		}

		GlobalPosition = spot;
		Velocity = Vector3.Zero;
		EnterIdle();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_initialized)
		{
			return;
		}

		var dt = (float)delta;
		if (_activityCooldownRemaining > 0.0f)
		{
			_activityCooldownRemaining -= dt;
		}

		switch (Resident.State)
		{
			case NpcRoutineState.Idle:
				TickIdle(dt);
				ApplyIdleMotion(dt);
				break;
			case NpcRoutineState.Wandering:
			case NpcRoutineState.GoingToBuilding:
			case NpcRoutineState.GoingToActivity:
				TickTravel(dt);
				break;
			case NpcRoutineState.AligningToActivity:
				TickAlignToActivity(dt);
				break;
			case NpcRoutineState.PerformingActivity:
				TickPerformingActivity(dt);
				break;
			case NpcRoutineState.LeavingActivity:
				TickLeavingActivity(dt);
				break;
			case NpcRoutineState.InsideBuilding:
				TickInside(dt);
				break;
			case NpcRoutineState.LeavingBuilding:
				TickLeaving(dt);
				break;
		}
	}

	private void TickIdle(float dt)
	{
		_waitRemaining -= dt;
		if (_waitRemaining > 0.0f)
		{
			return;
		}

		if (!DecideNext())
		{
			_waitRemaining = Range(_config.MinIdleSeconds, _config.MaxIdleSeconds);
		}
	}

	private bool DecideNext()
	{
		if (_preferWander)
		{
			_preferWander = false;
			if (TryBeginWander())
			{
				return true;
			}
		}
		else
		{
			var roll = _rng.NextDouble();
			if (roll < _config.VisitChance && TryBeginVisit())
			{
				return true;
			}

			if (roll < _config.VisitChance + _config.ActivityChance && TryBeginActivity())
			{
				return true;
			}
		}

		return TryBeginWander();
	}

	private bool TryBeginActivity()
	{
		if (_activityCooldownRemaining > 0.0f || _activityPoint != null)
		{
			return false;
		}

		if (!TryGetStartCell(out var start))
		{
			return false;
		}

		_population.CollectReachableActivities(start, _activityOptions, _rng);
		LimitFishingChoices();
		if (_activityOptions.Count == 0)
		{
			return false;
		}

		var totalWeight = 0.0f;
		foreach (var option in _activityOptions)
		{
			totalWeight += ActivityWeight(option);
		}

		if (totalWeight <= 0.0f)
		{
			return false;
		}

		var roll = (float)_rng.NextDouble() * totalWeight;
		NpcInteractionPoint pick = _activityOptions[^1];
		foreach (var option in _activityOptions)
		{
			roll -= ActivityWeight(option);
			if (roll <= 0.0f)
			{
				pick = option;
				break;
			}
		}

		if (!_population.InteractionRegistry.TryReserve(pick, Resident.Id))
		{
			return false;
		}

		if (!TryCommitRoute(
			    pick.ApproachCell,
			    pick.ApproachWorld,
			    true,
			    NpcRoutineState.GoingToActivity,
			    null))
		{
			_population.InteractionRegistry.Release(pick, Resident.Id);
			return false;
		}

		_activityPoint = pick;
		return true;
	}

	private void LimitFishingChoices()
	{
		var benches = 0;
		foreach (var option in _activityOptions)
		{
			if (option.ActivityType == NpcActivityType.SitBench)
			{
				benches++;
			}
		}

		if (benches == 0)
		{
			return;
		}

		var fish = new List<NpcInteractionPoint>();
		for (var i = _activityOptions.Count - 1; i >= 0; i--)
		{
			if (_activityOptions[i].ActivityType != NpcActivityType.Fish)
			{
				continue;
			}

			fish.Add(_activityOptions[i]);
			_activityOptions.RemoveAt(i);
		}

		fish.Sort((a, b) => ActivityDistance(a).CompareTo(ActivityDistance(b)));
		var keep = Math.Min(2, fish.Count);
		for (var i = 0; i < keep; i++)
		{
			_activityOptions.Add(fish[i]);
		}
	}

	private float ActivityDistance(NpcInteractionPoint point)
	{
		return HorizontalReach.DistanceXZ(GlobalPosition, point.ApproachWorld);
	}

	private float ActivityWeight(NpcInteractionPoint point)
	{
		var distance = ActivityDistance(point);
		if (point.ActivityType == NpcActivityType.SitBench)
		{
			var closeness = 1.0f + 8.0f / (1.0f + distance * 0.08f);
			return _config.SitWeight * closeness * 2.2f;
		}

		if (point.ActivityType == NpcActivityType.Fish)
		{
			return _config.FishWeight * (0.65f + 1.5f / (1.0f + distance * 0.1f));
		}

		return 1.0f;
	}

	private bool TryBeginVisit()
	{
		if (!TryGetStartCell(out var start))
		{
			return false;
		}

		_population.CollectReachableVisits(start, _visitOptions);
		if (_visitOptions.Count == 0)
		{
			return false;
		}

		var pick = _visitOptions[_rng.Next(_visitOptions.Count)];
		return TryCommitRoute(pick.Cell, pick.WorldPosition, true, NpcRoutineState.GoingToBuilding, pick.BuildingId);
	}

	private bool TryBeginWander()
	{
		if (!TryGetStartCell(out var start))
		{
			return false;
		}

		var grid = _population.Walkability.Grid;
		var margin = grid.CellCount - 1;
		for (var attempt = 0; attempt < 16; attempt++)
		{
			var cell = new Vector2I(_rng.Next(1, margin), _rng.Next(1, margin));
			if (!_population.Walkability.IsWalkable(cell))
			{
				continue;
			}

			var distance = Math.Abs(cell.X - start.X) + Math.Abs(cell.Y - start.Y);
			if (distance < 4)
			{
				continue;
			}

			var world = _population.Walkability.StandPoint(grid.CellToWorldCenter(cell));
			if (TryCommitRoute(cell, world, false, NpcRoutineState.Wandering, null))
			{
				return true;
			}
		}

		return false;
	}

	private void TickTravel(float dt)
	{
		if (!_hasPath || _waypointIndex >= _waypoints.Count)
		{
			OnArrived();
			return;
		}

		var target = _waypoints[_waypointIndex];
		var offset = target - GlobalPosition;
		offset.Y = 0.0f;
		if (offset.Length() <= _config.ArrivalRadius)
		{
			_waypointIndex++;
			_stuckTime = 0.0f;
			_stuckAnchor = GlobalPosition;
			if (_waypointIndex >= _waypoints.Count)
			{
				OnArrived();
			}

			return;
		}

		MoveToward(offset, dt);
		TrackStuck(dt);
	}

	private void OnArrived()
	{
		if (Resident.State == NpcRoutineState.GoingToBuilding)
		{
			EnterBuilding();
			return;
		}

		if (Resident.State == NpcRoutineState.GoingToActivity)
		{
			BeginAlignToActivity();
			return;
		}

		EnterIdle();
	}

	private void BeginAlignToActivity()
	{
		if (_activityPoint == null || !_activityPoint.IsValid())
		{
			CancelActivity(false);
			EnterIdle();
			return;
		}

		Resident.State = NpcRoutineState.AligningToActivity;
		_hasPath = false;
		_waypoints.Clear();
		_alignRemaining = _activityPoint.ActivityType == NpcActivityType.SitBench
			? Mathf.Max(_config.ActivityAlignSeconds, 1.6f)
			: 0.35f;
		_phaseElapsed = 0.0f;
		_standUpFinished = false;
		_lockActivityBody = false;
		Velocity = Vector3.Zero;
	}

	private void TickAlignToActivity(float dt)
	{
		if (_activityPoint == null || !_activityPoint.IsValid())
		{
			CancelActivity(false);
			EnterIdle();
			return;
		}

		_alignRemaining -= dt;
		_phaseElapsed += dt;
		var targetYaw = _activityPoint.ActivityYawRadians;
		var blend = 1.0f - Mathf.Exp(-_config.RotationSmoothness * dt);
		_visualYaw = Mathf.LerpAngle(_visualYaw, targetYaw, blend);
		_visualRoot.Basis = new Basis(Vector3.Up, _visualYaw);

		if (_activityPoint.ActivityType == NpcActivityType.SitBench)
		{
			if (!_lockActivityBody)
			{
				_lockActivityBody = true;
				_activityHoldPosition = GlobalPosition;
				SetActivityCollision(false);
				Visible = true;
			}

			var seat = _population.Walkability.StandPoint(_activityPoint.ActivityWorld);
			SlideBodyToward(seat, dt, 1.8f);
			var remaining = HorizontalReach.DistanceXZ(GlobalPosition, seat);
			if (remaining > 0.1f && _alignRemaining > 0.0f && _phaseElapsed < 2.2f)
			{
				return;
			}

			PlaceBody(seat);
		}

		Velocity = Vector3.Zero;
		_visualYaw = targetYaw;
		_visualRoot.Basis = new Basis(Vector3.Up, _visualYaw);
		BeginPerformingActivity();
	}

	private void BeginPerformingActivity()
	{
		if (_activityPoint == null)
		{
			EnterIdle();
			return;
		}

		_population.InteractionRegistry.MarkOccupied(_activityPoint, Resident.Id);
		Resident.State = NpcRoutineState.PerformingActivity;
		_activityPhase = ActivityPerformPhase.None;

		if (_activityPoint.ActivityType == NpcActivityType.SitBench)
		{
			SetActivityCollision(false);
			Visible = true;
			_activityPerformRemaining = Range(_config.MinSitSeconds, _config.MaxSitSeconds);
			SetActivityPhase(ActivityPerformPhase.SitDown);
			_activityVisual?.BeginSitDown(() => SetActivityPhase(ActivityPerformPhase.Sitting));
			return;
		}

		_lockActivityBody = false;
		SetActivityCollision(true);
		_activityPerformRemaining = Range(_config.MinFishSeconds, _config.MaxFishSeconds);
		SetActivityPhase(ActivityPerformPhase.FishCast);
		var water = FishingCastTarget();
		_activityVisual?.BeginFishCast(water, null);
	}

	private Vector3 FishingCastTarget()
	{
		var forward = new Vector3(Mathf.Sin(_visualYaw), 0.0f, Mathf.Cos(_visualYaw));
		var target = _population.Walkability.StandPoint(GlobalPosition + forward * 1.85f);
		target.Y += 0.22f;
		return target;
	}

	private void SetActivityPhase(ActivityPerformPhase phase)
	{
		_activityPhase = phase;
		_phaseElapsed = 0.0f;
	}

	private void TickPerformingActivity(float dt)
	{
		if (_activityPoint == null || !_activityPoint.IsValid())
		{
			BeginLeaveActivity();
			return;
		}

		_phaseElapsed += dt;
		switch (_activityPhase)
		{
			case ActivityPerformPhase.SitDown:
				HoldActivityBody();
				FaceActivityYaw();
				if (_phaseElapsed > 1.15f)
				{
					SetActivityPhase(ActivityPerformPhase.Sitting);
				}

				break;
			case ActivityPerformPhase.Sitting:
				_activityPerformRemaining -= dt;
				HoldActivityBody();
				FaceActivityYaw();
				_activityVisual?.TickSitIdle(dt);
				if (_activityPerformRemaining <= 0.0f)
				{
					BeginLeaveActivity();
				}

				break;
			case ActivityPerformPhase.FishCast:
				ApplyIdleMotion(dt);
				if ((_activityVisual != null && _activityVisual.TickFishCast(dt)) || _phaseElapsed > 1.4f)
				{
					SetActivityPhase(ActivityPerformPhase.FishWait);
				}

				break;
			case ActivityPerformPhase.FishWait:
				_activityPerformRemaining -= dt;
				_activityVisual?.TickFishIdle(dt);
				ApplyIdleMotion(dt);
				if (_activityPerformRemaining <= 0.0f)
				{
					SetActivityPhase(ActivityPerformPhase.FishReel);
					_activityVisual?.BeginFishReel(null);
				}

				break;
			case ActivityPerformPhase.FishReel:
				ApplyIdleMotion(dt);
				if ((_activityVisual != null && _activityVisual.TickFishReel(dt)) || _phaseElapsed > 1.2f)
				{
					BeginLeaveActivity();
				}

				break;
		}
	}

	private void BeginLeaveActivity()
	{
		Resident.State = NpcRoutineState.LeavingActivity;
		_phaseElapsed = 0.0f;
		_standUpFinished = false;
		if (_activityPoint?.ActivityType == NpcActivityType.SitBench)
		{
			SetActivityPhase(ActivityPerformPhase.StandUp);
			_activityVisual?.BeginStandUp(() => _standUpFinished = true);
			return;
		}

		_activityVisual?.CancelImmediate();
		FinishLeaveActivity();
	}

	private void TickLeavingActivity(float dt)
	{
		_phaseElapsed += dt;
		if (_activityPoint?.ActivityType != NpcActivityType.SitBench)
		{
			Velocity = Vector3.Zero;
			if (_phaseElapsed > 0.4f)
			{
				FinishLeaveActivity();
			}

			return;
		}

		if (!_standUpFinished && _phaseElapsed < 1.3f)
		{
			HoldActivityBody();
			FaceActivityYaw();
			return;
		}

		var exit = _population.Walkability.StandPoint(_activityPoint.ApproachWorld);
		SlideBodyToward(exit, dt, 2.4f);
		if (HorizontalReach.DistanceXZ(GlobalPosition, exit) <= 0.18f || _phaseElapsed > 3.2f)
		{
			PlaceBody(exit);
			FinishLeaveActivity();
		}
	}

	private void FinishLeaveActivity()
	{
		if (_lockActivityBody && _activityPoint?.ActivityType == NpcActivityType.SitBench)
		{
			PlaceBody(_population.Walkability.StandPoint(_activityPoint.ApproachWorld));
		}

		_lockActivityBody = false;
		SetActivityCollision(true);
		Velocity = Vector3.Zero;
		_activityCooldownRemaining = _config.ActivityCooldownSeconds;
		ReleaseActivityReservation();
		_preferWander = true;
		EnterIdle();
	}

	private void CancelActivity(bool invalidateCooldown)
	{
		if (_lockActivityBody && _activityPoint?.ActivityType == NpcActivityType.SitBench)
		{
			PlaceBody(_population.Walkability.StandPoint(_activityPoint.ApproachWorld));
		}

		_lockActivityBody = false;
		_standUpFinished = false;
		_activityVisual?.CancelImmediate();
		SetActivityCollision(true);
		Velocity = Vector3.Zero;
		ReleaseActivityReservation();
		if (invalidateCooldown)
		{
			_activityCooldownRemaining = _config.ActivityCooldownSeconds * 0.35f;
		}
	}

	private void SlideBodyToward(Vector3 target, float dt, float speed)
	{
		var next = GlobalPosition.MoveToward(target, speed * dt);
		PlaceBody(next);
	}

	private void PlaceBody(Vector3 position)
	{
		GlobalPosition = position;
		Velocity = Vector3.Zero;
		_activityHoldPosition = position;
	}

	private void FaceActivityYaw()
	{
		if (_activityPoint == null)
		{
			return;
		}

		_visualYaw = _activityPoint.ActivityYawRadians;
		_visualRoot.Basis = new Basis(Vector3.Up, _visualYaw);
	}

	private void HoldActivityBody()
	{
		Velocity = Vector3.Zero;
		if (_lockActivityBody)
		{
			GlobalPosition = _activityHoldPosition;
		}
	}

	private void ReleaseActivityReservation()
	{
		if (_activityPoint != null)
		{
			_population.InteractionRegistry.Release(_activityPoint, Resident.Id);
		}

		_activityPoint = null;
		_activityPhase = ActivityPerformPhase.None;
	}

	private void SetActivityCollision(bool enabled)
	{
		_activityCollisionDisabled = !enabled;
		if (_collisionShape != null)
		{
			_collisionShape.Disabled = !enabled || !Visible;
		}
	}

	private void EnterBuilding()
	{
		Resident.CurrentBuildingId = _targetBuildingId;
		Resident.State = NpcRoutineState.InsideBuilding;
		_hasPath = false;
		_waypoints.Clear();
		SetPresent(false);
		_insideRemaining = Range(_config.MinVisitSeconds, _config.MaxVisitSeconds);
		_leaveWait = 0.0f;
		_preferWander = true;
	}

	private void TickInside(float dt)
	{
		_insideRemaining -= dt;
		if (_insideRemaining > 0.0f)
		{
			return;
		}

		Resident.State = NpcRoutineState.LeavingBuilding;
		_leaveWait = 0.0f;
	}

	private void TickLeaving(float dt)
	{
		var building = _population.FindBuilding(Resident.CurrentBuildingId);
		if (building == null)
		{
			if (!_population.TryFindStandPoint(GlobalPosition, this, out var emergency))
			{
				emergency = _population.Walkability.StandPoint(GlobalPosition);
			}

			AppearAt(emergency);
			return;
		}

		var hasApproach = BuildingEntrance.TryResolve(building, _population.Walkability, out var approach);
		if (hasApproach && _population.IsStandClear(approach.WorldPosition, this))
		{
			AppearAt(approach.WorldPosition);
			return;
		}

		_leaveWait += dt;
		if (_leaveWait < _config.ExitRetrySeconds)
		{
			return;
		}

		var origin = hasApproach ? approach.WorldPosition : building.Visual.GlobalPosition;
		if (_population.TryFindStandPoint(origin, this, out var spot))
		{
			AppearAt(spot);
			return;
		}

		_leaveWait = 0.0f;
	}

	private void AppearAt(Vector3 world)
	{
		world = _population.Walkability.StandPoint(world);
		GlobalPosition = world;
		SetPresent(true);
		Resident.CurrentBuildingId = null;
		Velocity = Vector3.Zero;
		_leaveWait = 0.0f;
		EnterIdle();
	}

	private void EnterIdle()
	{
		if (Resident.State is NpcRoutineState.GoingToActivity or NpcRoutineState.AligningToActivity or NpcRoutineState.PerformingActivity)
		{
			CancelActivity(false);
		}

		Resident.State = NpcRoutineState.Idle;
		Resident.CurrentBuildingId = null;
		_targetBuildingId = null;
		_hasPath = false;
		_waypoints.Clear();
		_waypointIndex = 0;
		_stuckTime = 0.0f;
		_stuckRepaths = 0;
		_avoidCount = 0;
		_avoidUntilMs = 0;
		_waitRemaining = Range(_config.MinIdleSeconds, _config.MaxIdleSeconds);
		Velocity = new Vector3(0.0f, Velocity.Y, 0.0f);
	}

	private bool TryRepath()
	{
		if (Resident.State == NpcRoutineState.GoingToActivity)
		{
			if (_activityPoint == null || !_activityPoint.IsValid())
			{
				CancelActivity(false);
				return false;
			}

			return TryCommitRoute(
				_activityPoint.ApproachCell,
				_activityPoint.ApproachWorld,
				true,
				NpcRoutineState.GoingToActivity,
				null);
		}

		if (Resident.State == NpcRoutineState.GoingToBuilding)
		{
			var building = _population.FindBuilding(_targetBuildingId);
			if (building == null || !BuildingEntrance.TryResolve(building, _population.Walkability, out var approach))
			{
				return false;
			}

			return TryCommitRoute(
				approach.Cell,
				approach.WorldPosition,
				true,
				NpcRoutineState.GoingToBuilding,
				approach.BuildingId);
		}

		var world = _population.Walkability.StandPoint(_population.Walkability.Grid.CellToWorldCenter(_goalCell));
		return TryCommitRoute(_goalCell, world, false, NpcRoutineState.Wandering, null);
	}

	private bool TryCommitRoute(
		Vector2I goalCell,
		Vector3 exactGoal,
		bool replaceLastWithExact,
		NpcRoutineState nextState,
		string buildingId)
	{
		if (!TryGetStartCell(out var start))
		{
			return false;
		}

		if (!NpcGridPathfinder.TryFindPath(start, goalCell, _population.Walkability.IsWalkable, _cellPath, _population.Walkability.CanTraverse))
		{
			return false;
		}

		_pending.Clear();
		var grid = _population.Walkability.Grid;
		foreach (var cell in _cellPath)
		{
			var point = _population.Walkability.StandPoint(grid.CellToWorldCenter(cell));
			_pending.Add(point);
		}

		if (replaceLastWithExact && _pending.Count > 0)
		{
			_pending[^1] = _population.Walkability.StandPoint(exactGoal);
		}

		if (_pending.Count > 1 && HorizontalReach.DistanceXZ(GlobalPosition, _pending[0]) <= _config.ArrivalRadius)
		{
			_pending.RemoveAt(0);
		}

		if (_pending.Count == 0)
		{
			return false;
		}

		_waypoints.Clear();
		_waypoints.AddRange(_pending);
		_waypointIndex = 0;
		_hasPath = true;
		_goalCell = goalCell;
		_targetBuildingId = buildingId;
		Resident.State = nextState;
		_stuckTime = 0.0f;
		_stuckRepaths = 0;
		_stuckAnchor = GlobalPosition;
		return true;
	}

	private bool PathStillValid()
	{
		if (!_hasPath)
		{
			return false;
		}

		var walk = _population.Walkability;
		for (var i = _waypointIndex; i < _waypoints.Count; i++)
		{
			if (!walk.IsWalkable(walk.Grid.WorldToCell(_waypoints[i])))
			{
				return false;
			}
		}

		return true;
	}

	private bool TryGetStartCell(out Vector2I cell)
	{
		cell = _population.Walkability.Grid.WorldToCell(GlobalPosition);
		if (_population.Walkability.IsWalkable(cell))
		{
			return true;
		}

		return _population.TryFindNearestWalkable(GlobalPosition, 6, out cell);
	}

	private void TrackStuck(float dt)
	{
		var moved = HorizontalReach.DistanceXZ(_stuckAnchor, GlobalPosition);
		if (moved < 0.04f)
		{
			_stuckTime += dt;
		}
		else
		{
			_stuckTime = 0.0f;
			_stuckRepaths = 0;
			_avoidCount = 0;
			_stuckAnchor = GlobalPosition;
		}

		if (_stuckTime < 1.25f)
		{
			return;
		}

		_stuckTime = 0.0f;
		_stuckAnchor = GlobalPosition;
		_stuckRepaths++;
		if (_stuckRepaths >= 2 || !TryRepath())
		{
			_stuckRepaths = 0;
			EnterIdle();
		}
	}

	private void MoveToward(Vector3 offset, float dt)
	{
		var desired = offset.Normalized();
		var steering = Time.GetTicksMsec() < _avoidUntilMs ? _avoidDirection : AvoidResidents(desired);
		var before = GlobalPosition;
		var vertical = VerticalVelocity(dt);
		Velocity = new Vector3(steering.X * _speed, vertical, steering.Z * _speed);
		MoveAndSlide();
		Face(steering, dt);

		if (Time.GetTicksMsec() < _avoidUntilMs)
		{
			return;
		}

		var moved = HorizontalReach.DistanceXZ(before, GlobalPosition);
		if (!TouchedSomeone() || moved >= _speed * dt * 0.4f)
		{
			return;
		}

		StartAvoidance(desired);
	}

	private Vector3 AvoidResidents(Vector3 desired)
	{
		var push = Vector3.Zero;
		foreach (var node in GetTree().GetNodesInGroup(GroupName))
		{
			if (node is not NpcController other || other == this || !other.BlocksPlacement)
			{
				continue;
			}

			var away = GlobalPosition - other.GlobalPosition;
			away.Y = 0.0f;
			var distance = away.Length();
			if (distance < 0.001f || distance > 1.15f)
			{
				continue;
			}

			push += away / distance * (1.15f - distance);
		}

		if (push.LengthSquared() < 0.0001f)
		{
			return desired;
		}

		var steered = desired + push.Normalized() * Mathf.Clamp(push.Length() * 1.8f, 0.0f, 1.7f);
		steered.Y = 0.0f;
		return steered.LengthSquared() > 0.0001f ? steered.Normalized() : desired;
	}

	private void StartAvoidance(Vector3 blockedDirection)
	{
		_avoidCount++;
		if (_avoidCount >= 3)
		{
			_avoidCount = 0;
			_avoidUntilMs = 0;
			if (Resident.State is NpcRoutineState.GoingToActivity
			    or NpcRoutineState.AligningToActivity
			    or NpcRoutineState.PerformingActivity)
			{
				CancelActivity(false);
			}

			_preferWander = true;
			if (!TryBeginWander())
			{
				EnterIdle();
			}

			return;
		}

		var left = new Vector3(-blockedDirection.Z, 0.0f, blockedDirection.X);
		var right = -left;
		var leftOpen = SideIsOpen(left);
		var rightOpen = SideIsOpen(right);
		if (leftOpen && !rightOpen)
		{
			_avoidDirection = left;
		}
		else if (rightOpen && !leftOpen)
		{
			_avoidDirection = right;
		}
		else if (leftOpen && rightOpen)
		{
			_avoidDirection = StableSideSign() > 0 ? left : right;
		}
		else
		{
			_avoidDirection = -blockedDirection;
		}

		var duration = _avoidDirection == -blockedDirection ? 900ul : 650ul;
		_avoidUntilMs = Time.GetTicksMsec() + duration;
	}

	private bool SideIsOpen(Vector3 side)
	{
		if (side.LengthSquared() < 0.0001f)
		{
			return false;
		}

		side = side.Normalized();
		var probe = GlobalPosition + side * 0.8f;
		if (!_population.Walkability.IsWalkable(_population.Walkability.Grid.WorldToCell(probe)))
		{
			return false;
		}

		var from = GlobalPosition + new Vector3(0.0f, 0.45f, 0.0f);
		var to = probe + new Vector3(0.0f, 0.45f, 0.0f);
		var query = PhysicsRayQueryParameters3D.Create(from, to, CollisionLayers.Structure);
		query.CollideWithAreas = false;
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		return GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	private bool TouchedSomeone()
	{
		var count = GetSlideCollisionCount();
		for (var i = 0; i < count; i++)
		{
			var collider = GetSlideCollision(i).GetCollider();
			if (collider is NpcController or PlayerController)
			{
				return true;
			}
		}

		return false;
	}

	private int StableSideSign()
	{
		var id = Resident?.Id ?? string.Empty;
		var hash = 17;
		foreach (var character in id)
		{
			hash = hash * 31 + character;
		}

		return (hash & 1) == 0 ? 1 : -1;
	}

	private void ApplyIdleMotion(float dt)
	{
		Velocity = new Vector3(0.0f, VerticalVelocity(dt), 0.0f);
		MoveAndSlide();
	}

	private float VerticalVelocity(float dt)
	{
		var vertical = Velocity.Y;
		if (!IsOnFloor())
		{
			vertical += (float)GetGravity().Y * dt;
		}
		else if (vertical < 0.0f)
		{
			vertical = 0.0f;
		}

		return vertical;
	}

	private void Face(Vector3 direction, float dt)
	{
		var targetYaw = Mathf.Atan2(direction.X, direction.Z);
		var blend = 1.0f - Mathf.Exp(-_config.RotationSmoothness * dt);
		_visualYaw = Mathf.LerpAngle(_visualYaw, targetYaw, blend);
		_visualRoot.Basis = new Basis(Vector3.Up, _visualYaw);
	}

	private void SetPresent(bool present)
	{
		Visible = present;
		_collisionShape.Disabled = !present || _activityCollisionDisabled;
		CollisionLayer = present ? CollisionLayers.Npc : 0;
		CollisionMask = present ? CollisionLayers.NpcMask : 0;
		if (!present)
		{
			Velocity = Vector3.Zero;
		}
	}

	private float Range(float min, float max)
	{
		if (max < min)
		{
			(min, max) = (max, min);
		}

		if (Mathf.IsEqualApprox(min, max))
		{
			return min;
		}

		return min + (float)_rng.NextDouble() * (max - min);
	}

	private enum ActivityPerformPhase
	{
		None,
		SitDown,
		Sitting,
		StandUp,
		FishCast,
		FishWait,
		FishReel,
	}
}
