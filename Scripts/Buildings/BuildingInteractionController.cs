using Godot;

namespace Vilanova;

/// <summary>
/// F gathers, sits, or fishes. Z inspects the nearest building.
/// </summary>
public partial class BuildingInteractionController : Node
{
	[Export] public float InteractionRadius { get; set; } = 3.0f;
	[Export] public float StrikeCooldownSeconds { get; set; } = 0.42f;
	[Export] public NodePath BuildingManagerPath { get; set; } = new("../BuildingManager");
	[Export] public NodePath PlayerPath { get; set; } = new("../Player");
	[Export] public NodePath InfoPanelPath { get; set; } = new("../BuildingInfoPanelUI");
	[Export] public NodePath IndicatorPath { get; set; } = new("../InteractionIndicator");

	private BuildingManager _buildingManager = null!;
	private PlayerController _player = null!;
	private BuildingInfoPanelUI _infoPanel = null!;
	private InteractionIndicatorVisual _indicator = null!;
	private GatherToolVisual _tool = null!;
	private Node3D _drops = null!;
	private PlayerLeisure _leisure = null!;
	private ulong _nextStrikeMs;
	private float _harvestDistance = float.MaxValue;

	public PlacedBuilding NearestTarget { get; private set; }
	public HarvestableResource NearestHarvest { get; private set; }

	public override void _Ready()
	{
		_buildingManager = GetNode<BuildingManager>(BuildingManagerPath);
		_player = GetNode<PlayerController>(PlayerPath);
		_infoPanel = GetNode<BuildingInfoPanelUI>(InfoPanelPath);
		_indicator = GetNode<InteractionIndicatorVisual>(IndicatorPath);
		_drops = new Node3D { Name = "GatherDrops" };
		AddChild(_drops);

		_tool = new GatherToolVisual
		{
			Name = "GatherTool",
			Position = new Vector3(0.22f, 0.95f, 0.42f),
		};
		_player.VisualRoot.AddChild(_tool);
		_leisure = _player.GetNode<PlayerLeisure>("Leisure");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsEcho())
		{
			return;
		}

		if (@event.IsActionPressed("build_inspect"))
		{
			TryInspect();
			return;
		}

		if (!@event.IsActionPressed("build_interact"))
		{
			return;
		}

		if (_leisure.IsActive)
		{
			_leisure.RequestEnd();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_infoPanel.IsOpen || _buildingManager.Mode != BuildingMode.Idle)
		{
			return;
		}

		if (_leisure.HasOffer && (NearestHarvest == null || _leisure.OfferDistance + 0.08f < _harvestDistance))
		{
			_leisure.TryBeginOffer();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (NearestHarvest != null)
		{
			Strike(NearestHarvest);
			GetViewport().SetInputAsHandled();
		}
	}

	private void TryInspect()
	{
		if (_leisure.IsActive || _infoPanel.IsOpen || _buildingManager.Mode != BuildingMode.Idle || NearestTarget == null)
		{
			return;
		}

		var center = _buildingManager.GetBuildingFootprintCenter(NearestTarget);
		_infoPanel.ShowBuilding(NearestTarget, center);
		GetViewport().SetInputAsHandled();
	}

	public override void _Process(double delta)
	{
		NearestHarvest = null;
		NearestTarget = null;
		_harvestDistance = float.MaxValue;
		if (_leisure.IsActive)
		{
			_indicator.ShowPrompt(_player.GlobalPosition + new Vector3(0.0f, 1.35f, 0.0f), "F — Levantar");
			return;
		}

		if (_infoPanel.IsOpen || _buildingManager.Mode != BuildingMode.Idle)
		{
			_indicator.HideIndicator();
			return;
		}

		ChooseNearest();
		if (_leisure.HasOffer && (NearestHarvest == null || _leisure.OfferDistance + 0.08f < _harvestDistance))
		{
			_indicator.ShowPrompt(_leisure.OfferAnchor + new Vector3(0.0f, 0.9f, 0.0f), _leisure.OfferPrompt);
			return;
		}

		if (NearestHarvest != null)
		{
			var center = _buildingManager.GetBuildingFootprintCenter(NearestHarvest.Placed);
			_indicator.ShowPrompt(center, NearestHarvest.Prompt);
			return;
		}

		if (NearestTarget == null)
		{
			_indicator.HideIndicator();
			return;
		}

		_indicator.ShowPrompt(_buildingManager.GetBuildingFootprintCenter(NearestTarget), "Z — Inspecionar construção");
	}

	private void ChooseNearest()
	{
		var origin = _player.GlobalPosition;
		float bestHarvestDistance = float.MaxValue;
		string bestHarvestId = null;
		float bestInspectDistance = float.MaxValue;
		string bestInspectId = null;

		foreach (var placed in _buildingManager.Occupancy.AllBuildings)
		{
			var harvest = HarvestableResource.Find(placed);
			if (harvest != null && harvest.IsDepleted)
			{
				continue;
			}

			var center = _buildingManager.GetBuildingFootprintCenter(placed);
			var distance = HorizontalDistance(origin, center);
			var isHarvest = harvest != null;
			var limit = isHarvest ? harvest.Range : InteractionRadius;
			if (distance > limit)
			{
				continue;
			}

			if (isHarvest && WallBlocks(center, placed.Visual))
			{
				continue;
			}

			var id = placed.InstanceId ?? string.Empty;
			if (isHarvest)
			{
				if (!InteractionArbiter.IsBetter(distance, true, id, bestHarvestDistance, true, bestHarvestId))
				{
					continue;
				}

				bestHarvestDistance = distance;
				bestHarvestId = id;
				NearestHarvest = harvest;
				_harvestDistance = distance;
				continue;
			}

			if (!InteractionArbiter.IsBetter(distance, false, id, bestInspectDistance, false, bestInspectId))
			{
				continue;
			}

			bestInspectDistance = distance;
			bestInspectId = id;
			NearestTarget = placed;
		}
	}

	private void Strike(HarvestableResource harvest)
	{
		var now = Time.GetTicksMsec();
		if (now < _nextStrikeMs || harvest.IsDepleted)
		{
			return;
		}

		_nextStrikeMs = now + (ulong)(StrikeCooldownSeconds * 1000.0f);
		var center = _buildingManager.GetBuildingFootprintCenter(harvest.Placed);
		_player.FaceTowards(center, 0.32f);
		_tool.Play(harvest.Tool);
		harvest.ReceiveStrike(_drops);
	}

	private bool WallBlocks(Vector3 target, Node3D visual)
	{
		var from = _player.GlobalPosition + new Vector3(0, 0.7f, 0);
		var to = target + new Vector3(0, 0.7f, 0);
		var query = PhysicsRayQueryParameters3D.Create(from, to, CollisionLayers.Structure);
		query.CollideWithAreas = false;
		var exclude = new Godot.Collections.Array<Rid>();
		CollectBodies(_player, exclude);
		CollectBodies(visual, exclude);
		query.Exclude = exclude;
		var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0;
	}

	private static void CollectBodies(Node node, Godot.Collections.Array<Rid> exclude)
	{
		if (node is CollisionObject3D body)
		{
			exclude.Add(body.GetRid());
		}

		foreach (var child in node.GetChildren())
		{
			CollectBodies(child, exclude);
		}
	}

	private static float HorizontalDistance(Vector3 a, Vector3 b)
	{
		var dx = a.X - b.X;
		var dz = a.Z - b.Z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}
}
