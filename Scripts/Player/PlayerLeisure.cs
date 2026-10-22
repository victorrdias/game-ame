using Godot;

namespace Vilanova;

/// <summary>
/// Player sit and fish. F starts the nearest free bench or shore; F or movement ends it.
/// The body is held in place so sitting does not fall through the bench.
/// </summary>
public partial class PlayerLeisure : Node
{
	public const string ActorId = "player";

	private const float SitRange = 1.85f;
	private const float FishRange = 0.7f;
	private const float FishEdgeOffset = 0.95f;

	private PlayerController _player = null!;
	private CollisionShape3D _collision = null!;
	private NpcActivityVisual _activity = null!;
	private NpcPopulationManager _population;
	private NpcInteractionPoint _point;
	private NpcInteractionPoint _offer;
	private Vector3 _returnPosition;
	private float _phaseTime;
	private Phase _phase = Phase.None;

	public bool IsActive => _phase != Phase.None;
	public bool HasOffer => _offer != null && !IsActive;
	public float OfferDistance { get; private set; }
	public string OfferPrompt { get; private set; } = string.Empty;
	public Vector3 OfferAnchor { get; private set; }

	public override void _Ready()
	{
		_player = GetParent<PlayerController>();
		_collision = _player.GetNode<CollisionShape3D>("CollisionShape3D");
		_activity = _player.GetNode<NpcActivityVisual>("VisualRoot/Activity");
	}

	public override void _Process(double delta)
	{
		if (!IsActive)
		{
			RefreshOffer();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsActive)
		{
			return;
		}

		var dt = (float)delta;
		_phaseTime += dt;
		if (_point == null || !_point.IsValid())
		{
			Finish(true);
			return;
		}

		switch (_phase)
		{
			case Phase.SitMove:
				TickSitMove(dt);
				break;
			case Phase.SitDown:
				HoldSeat();
				if (_phaseTime > 1.15f)
				{
					_phase = Phase.Sitting;
					_phaseTime = 0.0f;
				}

				break;
			case Phase.Sitting:
				HoldSeat();
				_activity.TickSitIdle(dt);
				if (_player.HasMoveInput)
				{
					BeginStand();
				}

				break;
			case Phase.StandUp:
				HoldSeat();
				if (_phaseTime > 1.35f)
				{
					Finish(true);
				}

				break;
			case Phase.FishCast:
				HoldFacing();
				if (_activity.TickFishCast(dt) || _phaseTime > 1.4f)
				{
					_phase = Phase.FishIdle;
					_phaseTime = 0.0f;
				}

				break;
			case Phase.FishIdle:
				HoldFacing();
				_activity.TickFishIdle(dt);
				if (_player.HasMoveInput)
				{
					BeginReel();
				}

				break;
			case Phase.FishReel:
				HoldFacing();
				if (_activity.TickFishReel(dt) || _phaseTime > 1.2f)
				{
					Finish(false);
				}

				break;
		}
	}

	public bool TryBeginOffer()
	{
		if (IsActive || _offer == null || _population?.InteractionRegistry == null)
		{
			return false;
		}

		if (!_population.InteractionRegistry.TryReserve(_offer, ActorId))
		{
			_offer = null;
			return false;
		}

		_point = _offer;
		_offer = null;
		_returnPosition = _player.GlobalPosition;
		_phaseTime = 0.0f;
		_population.InteractionRegistry.MarkOccupied(_point, ActorId);
		if (_point.ActivityType == NpcActivityType.SitBench)
		{
			_phase = Phase.SitMove;
			_collision.Disabled = true;
			_player.SetLeisureHold(_player.GlobalPosition, _point.ActivityYawRadians);
			return true;
		}

		_phase = Phase.FishCast;
		var water = CastTarget();
		_player.SetLeisureHold(_player.GlobalPosition, _point.ActivityYawRadians);
		_activity.BeginFishCast(water, null);
		return true;
	}

	public void RequestEnd()
	{
		switch (_phase)
		{
			case Phase.SitMove:
			case Phase.SitDown:
			case Phase.Sitting:
				BeginStand();
				break;
			case Phase.FishCast:
			case Phase.FishIdle:
				BeginReel();
				break;
		}
	}

	private void TickSitMove(float dt)
	{
		if (_player.HasMoveInput)
		{
			Finish(false);
			return;
		}

		var seat = SeatPosition();
		var next = _player.GlobalPosition.MoveToward(seat, 2.2f * dt);
		_player.SetLeisureHold(next, _point.ActivityYawRadians);
		if (HorizontalReach.DistanceXZ(next, seat) <= 0.08f || _phaseTime > 0.7f)
		{
			_player.SetLeisureHold(seat, _point.ActivityYawRadians);
			_phase = Phase.SitDown;
			_phaseTime = 0.0f;
			_activity.BeginSitDown(() =>
			{
				if (_phase == Phase.SitDown)
				{
					_phase = Phase.Sitting;
					_phaseTime = 0.0f;
				}
			});
		}
	}

	private void BeginStand()
	{
		if (_phase == Phase.StandUp)
		{
			return;
		}

		_phase = Phase.StandUp;
		_phaseTime = 0.0f;
		_activity.BeginStandUp(() => Finish(true));
	}

	private void BeginReel()
	{
		if (_phase == Phase.FishReel)
		{
			return;
		}

		_phase = Phase.FishReel;
		_phaseTime = 0.0f;
		_activity.BeginFishReel(null);
	}

	private void HoldSeat()
	{
		_player.SetLeisureHold(SeatPosition(), _point.ActivityYawRadians);
	}

	private void HoldFacing()
	{
		var position = _player.GlobalPosition;
		position.Y = _returnPosition.Y;
		_player.SetLeisureHold(position, _point.ActivityYawRadians);
	}

	private Vector3 SeatPosition()
	{
		return _population.Walkability.StandPoint(_point.ActivityWorld);
	}

	private Vector3 CastTarget()
	{
		var yaw = _point.ActivityYawRadians;
		var forward = new Vector3(Mathf.Sin(yaw), 0.0f, Mathf.Cos(yaw));
		var target = _population.Walkability.StandPoint(_player.GlobalPosition + forward * 1.85f);
		target.Y += 0.22f;
		return target;
	}

	private void Finish(bool stepOffSeat)
	{
		var registry = _population?.InteractionRegistry;
		if (stepOffSeat && _point != null && _population?.Walkability != null)
		{
			var yaw = _point.ActivityYawRadians;
			var forward = new Vector3(Mathf.Sin(yaw), 0.0f, Mathf.Cos(yaw));
			var exit = _population.Walkability.StandPoint(_point.ActivityWorld + forward * 0.85f);
			_player.GlobalPosition = exit;
		}

		_activity.CancelImmediate();
		_collision.Disabled = false;
		_player.ClearLeisureHold();
		if (_point != null)
		{
			registry?.Release(_point, ActorId);
		}

		_point = null;
		_phase = Phase.None;
		_phaseTime = 0.0f;
	}

	private void RefreshOffer()
	{
		_offer = null;
		OfferPrompt = string.Empty;
		OfferDistance = float.MaxValue;
		if (!EnsurePopulation())
		{
			return;
		}

		NpcInteractionPoint bestSeat = null;
		var bestSeatDistance = float.MaxValue;
		NpcInteractionPoint bestShore = null;
		var bestShoreDistance = float.MaxValue;
		foreach (var point in _population.InteractionRegistry.AllPoints)
		{
			if (!point.IsUsable)
			{
				continue;
			}

			if (point.ActivityType == NpcActivityType.SitBench)
			{
				var seatDistance = HorizontalReach.DistanceXZ(_player.GlobalPosition, point.ActivityWorld);
				if (seatDistance <= SitRange && seatDistance < bestSeatDistance)
				{
					bestSeat = point;
					bestSeatDistance = seatDistance;
				}

				continue;
			}

			if (point.ActivityType != NpcActivityType.Fish)
			{
				continue;
			}

			var edge = FishEdge(point);
			var shoreDistance = HorizontalReach.DistanceXZ(_player.GlobalPosition, edge);
			if (shoreDistance <= FishRange && shoreDistance < bestShoreDistance)
			{
				bestShore = point;
				bestShoreDistance = shoreDistance;
			}
		}

		var best = bestSeat ?? bestShore;
		if (best == null)
		{
			return;
		}

		_offer = best;
		OfferDistance = best.ActivityType == NpcActivityType.SitBench ? bestSeatDistance : bestShoreDistance;
		OfferAnchor = best.ActivityType == NpcActivityType.SitBench ? best.ActivityWorld : FishEdge(best);
		OfferPrompt = best.ActivityType == NpcActivityType.SitBench ? "F — Sentar" : "F — Pescar";
	}

	private static Vector3 FishEdge(NpcInteractionPoint point)
	{
		var forward = new Vector3(Mathf.Sin(point.ActivityYawRadians), 0.0f, Mathf.Cos(point.ActivityYawRadians));
		return point.ActivityWorld + forward * FishEdgeOffset;
	}

	private bool EnsurePopulation()
	{
		if (_population != null && _population.InteractionRegistry != null)
		{
			return true;
		}

		_population = GetTree().Root.FindChild("NpcPopulationManager", true, false) as NpcPopulationManager;
		return _population?.InteractionRegistry != null;
	}

	private enum Phase
	{
		None,
		SitMove,
		SitDown,
		Sitting,
		StandUp,
		FishCast,
		FishIdle,
		FishReel,
	}
}
