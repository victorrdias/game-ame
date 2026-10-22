using Godot;

namespace Vilanova;

/// <summary>
/// Walkable cells are in-bounds and not occupied by something that blocks movement.
/// Paths and flowers stay walkable. The grid is read live, so placement updates navigation
/// without rebuilding a structure every frame.
/// </summary>
public sealed class TownWalkability
{
	public static readonly Vector2I[] CardinalOffsets =
	{
		new(1, 0),
		new(-1, 0),
		new(0, 1),
		new(0, -1),
	};

	public GridSystem Grid { get; }
	public BuildingOccupancyGrid Occupancy { get; }

	public TownWalkability(GridSystem grid, BuildingOccupancyGrid occupancy)
	{
		Grid = grid;
		Occupancy = occupancy;
	}

	public bool IsWalkable(Vector2I cell)
	{
		if (!Grid.IsCellInBounds(cell))
		{
			return false;
		}

		var placed = Occupancy.GetAtCell(cell);
		if (placed?.Definition == null)
		{
			return true;
		}

		return !placed.Definition.BlocksMovement;
	}
}
