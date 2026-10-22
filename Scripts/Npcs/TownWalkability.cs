using Godot;

namespace Vilanova;

/// <summary>
/// Navigation stays on the 2m visual grid. A visual cell is blocked when the agent
/// cannot stand on its center, or when most of its 3x3 subtiles block movement.
/// A single corner subtile does not seal the whole visual cell.
/// </summary>
public sealed class TownWalkability
{
	public const int MostlyBlockedSubtileCount = 6;

	public static readonly Vector2I[] CardinalOffsets =
	{
		new(1, 0),
		new(-1, 0),
		new(0, 1),
		new(0, -1),
	};

	public TownGridLayout Layout { get; }
	public GridSystem Grid => Layout.Visual;
	public BuildingOccupancyGrid Occupancy { get; }
	public TerrainHeightField Heights { get; }

	public TownWalkability(TownGridLayout layout, BuildingOccupancyGrid occupancy, TerrainHeightField heights = null)
	{
		Layout = layout;
		Occupancy = occupancy;
		Heights = heights ?? TerrainHeightField.Flat(layout.TerrainSize, layout.VisualCellSize);
	}

	public bool IsWalkable(Vector2I visualCell)
	{
		if (!Grid.IsCellInBounds(visualCell) || !Heights.IsStandable(visualCell))
		{
			return false;
		}

		return !BlocksNavigation(visualCell);
	}

	public bool BlocksNavigation(Vector2I visualCell)
	{
		var blocked = 0;
		var centerBlocked = false;
		for (var x = 0; x < TownGridLayout.Subdivisions; x++)
		{
			for (var z = 0; z < TownGridLayout.Subdivisions; z++)
			{
				var subtile = Layout.PlacementCellAtVisualLocal(visualCell, x, z);
				if (!SubtileBlocksMovement(subtile))
				{
					continue;
				}

				blocked++;
				if (x == 1 && z == 1)
				{
					centerBlocked = true;
				}
			}
		}

		return centerBlocked || blocked >= MostlyBlockedSubtileCount;
	}

	public bool CanTraverse(Vector2I from, Vector2I to)
	{
		return IsWalkable(from) && IsWalkable(to) && Heights.AllowsStep(from, to);
	}

	public Vector3 StandPoint(Vector3 world)
	{
		return Heights.WithSurface(world);
	}

	private bool SubtileBlocksMovement(Vector2I placementCell)
	{
		var placed = Occupancy.GetAtCell(placementCell);
		return placed?.Definition != null && placed.Definition.BlocksMovement;
	}
}
