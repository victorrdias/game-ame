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
		Resident.State != NpcRoutineState.LeavingBuilding;

	private NpcPopulationManager _population = null!;
	private NpcRoutineConfig _config = null!;
	private CollisionShape3D _collisionShape = null!;
	private Node3D _visualRoot = null!;
	private readonly List<Vector2I> _cellPath = new();
	private readonly List<Vector3> _waypoints = new();
	private readonly List<Vector3> _pending = new();
	private readonly List<BuildingApproach> _visitOptions = new();
	private Random _rng = null!;
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
	private bool _hasPath;
	private bool _preferWander;
	private bool _initialized;

	public override void _Ready()
	{
		_collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
		_visualRoot = GetNode<Node3D>("VisualRoot");
		MotionMode = MotionModeEnum.Grounded;
		FloorSnapLength = 0.25f;
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
		_visualYaw = (float)_rng.NextDouble() * 360.0f;
		_visualRoot.RotationDegrees = new Vector3(0, _visualYaw, 0);
		_stuckAnchor = GlobalPosition;
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

		if (Resident.State != NpcRoutineState.Wandering && Resident.State != NpcRoutineState.GoingToBuilding)
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
				spot = origin;
				spot.Y = 0.0f;
			}

			AppearAt(spot);
			return;
		}

		EnterIdle();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_initialized)
		{
			return;
		}

		var dt = (float)delta;
		switch (Resident.State)
		{
			case NpcRoutineState.Idle:
				TickIdle(dt);
				ApplyIdleMotion(dt);
				break;
			case NpcRoutineState.Wandering:
			case NpcRoutineState.GoingToBuilding:
				TickTravel(dt);
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
		else if (_rng.NextDouble() < _config.VisitChance && TryBeginVisit())
		{
			return true;
		}

		return TryBeginWander();
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

			var world = grid.CellToWorldCenter(cell);
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

		EnterIdle();
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
				emergency = GlobalPosition;
				emergency.Y = 0.0f;
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
		world.Y = 0.0f;
		GlobalPosition = world;
		SetPresent(true);
		Resident.CurrentBuildingId = null;
		Velocity = Vector3.Zero;
		_leaveWait = 0.0f;
		EnterIdle();
	}

	private void EnterIdle()
	{
		Resident.State = NpcRoutineState.Idle;
		Resident.CurrentBuildingId = null;
		_targetBuildingId = null;
		_hasPath = false;
		_waypoints.Clear();
		_waypointIndex = 0;
		_stuckTime = 0.0f;
		_stuckRepaths = 0;
		_waitRemaining = Range(_config.MinIdleSeconds, _config.MaxIdleSeconds);
		Velocity = new Vector3(0.0f, Velocity.Y, 0.0f);
	}

	private bool TryRepath()
	{
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

		var world = _population.Walkability.Grid.CellToWorldCenter(_goalCell);
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

		if (!NpcGridPathfinder.TryFindPath(start, goalCell, _population.Walkability.IsWalkable, _cellPath))
		{
			return false;
		}

		_pending.Clear();
		var grid = _population.Walkability.Grid;
		foreach (var cell in _cellPath)
		{
			var point = grid.CellToWorldCenter(cell);
			point.Y = 0.0f;
			_pending.Add(point);
		}

		if (replaceLastWithExact && _pending.Count > 0)
		{
			exactGoal.Y = 0.0f;
			_pending[^1] = exactGoal;
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
		var direction = offset.Normalized();
		var vertical = VerticalVelocity(dt);
		Velocity = new Vector3(direction.X * _speed, vertical, direction.Z * _speed);
		MoveAndSlide();
		Face(direction, dt);
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
		var targetYaw = Mathf.RadToDeg(Mathf.Atan2(direction.X, direction.Z));
		var blend = 1.0f - Mathf.Exp(-_config.RotationSmoothness * dt);
		_visualYaw = Mathf.LerpAngle(_visualYaw, targetYaw, blend);
		_visualRoot.RotationDegrees = new Vector3(0, _visualYaw, 0);
	}

	private void SetPresent(bool present)
	{
		Visible = present;
		_collisionShape.Disabled = !present;
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
}
