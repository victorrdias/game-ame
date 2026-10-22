using Godot;

namespace Vilanova;

/// <summary>
/// Per-instance chop or mine progress. Attached only when the catalog definition says the prop is a resource.
/// </summary>
public partial class HarvestableResource : Node
{
	public const string NodeName = "HarvestableResource";

	public PlacedBuilding Placed { get; private set; }
	public GatherKind Kind { get; private set; }
	public GatherTool Tool { get; private set; }
	public int RequiredHits { get; private set; } = 4;
	public int Hits => _progress?.Hits ?? 0;
	public int DropCount { get; private set; }
	public float Range { get; private set; } = 2.5f;
	public bool IsDepleted => _progress != null && _progress.IsDepleted;

	private BuildingManager _manager = null!;
	private HarvestProgress _progress = new(4);

	public static void Ensure(PlacedBuilding placed, BuildingManager manager)
	{
		if (placed?.Visual == null || placed.Definition == null || placed.Definition.GatherKind == GatherKind.None)
		{
			return;
		}

		if (placed.Visual.GetNodeOrNull<HarvestableResource>(NodeName) != null)
		{
			return;
		}

		var node = new HarvestableResource { Name = NodeName };
		placed.Visual.AddChild(node);
		node.Bind(placed, manager);
	}

	public static HarvestableResource Find(PlacedBuilding placed)
	{
		return placed?.Visual == null || !GodotObject.IsInstanceValid(placed.Visual)
			? null
			: placed.Visual.GetNodeOrNull<HarvestableResource>(NodeName);
	}

	public void Bind(PlacedBuilding placed, BuildingManager manager)
	{
		Placed = placed;
		_manager = manager;
		Kind = placed.Definition.GatherKind;
		Tool = Kind == GatherKind.Stone ? GatherTool.Pickaxe : GatherTool.Axe;
		RequiredHits = Mathf.Max(1, placed.Definition.GatherHits);
		_progress = new HarvestProgress(RequiredHits);
		Range = placed.Definition.GatherRange > 0.1f ? placed.Definition.GatherRange : 2.5f;
		DropCount = placed.Definition.GatherDropCount > 0
			? placed.Definition.GatherDropCount
			: Kind == GatherKind.Stone ? 2 : 3;
	}

	public string Prompt => Kind == GatherKind.Stone ? "F — Quebrar pedra" : "F — Cortar árvore";

	public void ReceiveStrike(Node3D dropsRoot)
	{
		if (Placed?.Visual == null || !_progress.TryHit(out var depleted))
		{
			return;
		}

		GatherStrikeFeedback.PlayHit(Placed.Visual, Kind, !depleted);
		if (!depleted)
		{
			return;
		}

		var origin = _manager.GetBuildingFootprintCenter(Placed);
		_manager.ReleaseFootprint(Placed);
		GatherDrop.Spawn(dropsRoot, _manager.Heights, origin, Kind, DropCount);
		GatherStrikeFeedback.PlayFell(Placed.Visual);
	}
}
