using Godot;

namespace Vilanova;

[GlobalClass]
public partial class BuildingDefinition : Resource
{
	[Export] public string Id { get; set; } = string.Empty;
	[Export] public string DisplayName { get; set; } = string.Empty;

	/// <summary>
	/// Optional catalog copy. An empty value gets a short line from the category.
	/// </summary>
	[Export] public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Optional card image. When empty, the build menu looks up a preview PNG and then a painted fallback.
	/// </summary>
	[Export] public Texture2D Thumbnail { get; set; }

	[Export] public BuildingCategory Category { get; set; } = BuildingCategory.Buildings;
	[Export] public OccupancyLayer Layer { get; set; } = OccupancyLayer.Structure;
	[Export] public Vector2I SizeInCells { get; set; } = Vector2I.One;
	[Export] public bool CanRotate { get; set; } = true;
	[Export] public bool CanDemolish { get; set; } = true;
	[Export] public bool BlocksMovement { get; set; } = true;

	/// <summary>
	/// None keeps the prop decorative. Wood and Stone become harvestable instances.
	/// </summary>
	[Export] public GatherKind GatherKind { get; set; } = GatherKind.None;

	[Export] public int GatherHits { get; set; } = 4;

	/// <summary>
	/// Zero uses 3 logs for wood and 2 chunks for stone.
	/// </summary>
	[Export] public int GatherDropCount { get; set; }

	[Export] public float GatherRange { get; set; } = 2.5f;

	/// <summary>
	/// Movement-block height in meters. Zero uses the size-based default.
	/// </summary>
	[Export] public float CollisionHeight { get; set; }

	/// <summary>
	/// Pieces with the same group drop the shared border when placed against each other.
	/// </summary>
	[Export] public string LinkGroup { get; set; } = string.Empty;

	/// <summary>
	/// Residents may walk to <see cref="EntranceLocalOffset"/> and wait inside.
	/// Paths, plants, and decorations stay false.
	/// </summary>
	[Export] public bool IsVisitable { get; set; }

	/// <summary>
	/// Door approach in the building's unrotated local space. Must sit outside structure collision.
	/// </summary>
	[Export] public Vector3 EntranceLocalOffset { get; set; }

	[Export] public PackedScene VisualScene { get; set; } = null!;
}
