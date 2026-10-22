using Godot;

namespace Vilanova;

[GlobalClass]
public partial class BuildingDefinition : Resource
{
	[Export] public string Id { get; set; } = string.Empty;
	[Export] public string DisplayName { get; set; } = string.Empty;
	[Export] public BuildingCategory Category { get; set; } = BuildingCategory.Buildings;
	[Export] public OccupancyLayer Layer { get; set; } = OccupancyLayer.Structure;
	[Export] public Vector2I SizeInCells { get; set; } = Vector2I.One;
	[Export] public bool CanRotate { get; set; } = true;
	[Export] public bool CanDemolish { get; set; } = true;
	[Export] public bool BlocksMovement { get; set; } = true;

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
