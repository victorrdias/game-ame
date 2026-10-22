using Godot;

namespace Vilanova;

/// <summary>
/// Shared origin for the 2m visual grid and the 2/3m placement grid.
/// One visual cell contains <see cref="Subdivisions"/> by <see cref="Subdivisions"/> placement cells.
/// </summary>
public sealed class TownGridLayout
{
	public const int Subdivisions = 3;

	public float TerrainSize { get; }
	public float VisualCellSize { get; }
	public float PlacementCellSize { get; }
	public GridSystem Visual { get; }
	public GridSystem Placement { get; }

	public TownGridLayout(float terrainSize, float visualCellSize = 2.0f)
	{
		TerrainSize = terrainSize;
		VisualCellSize = visualCellSize;
		PlacementCellSize = visualCellSize / Subdivisions;
		Visual = new GridSystem(terrainSize, visualCellSize);
		Placement = new GridSystem(terrainSize, PlacementCellSize);
	}

	public Vector2I WorldToVisual(Vector3 worldPosition)
	{
		return CellFromWorld(worldPosition, VisualCellSize);
	}

	public Vector2I WorldToPlacement(Vector3 worldPosition)
	{
		return CellFromWorld(worldPosition, PlacementCellSize);
	}

	public Vector3 VisualToWorldCenter(Vector2I visualCell)
	{
		return Visual.CellToWorldCenter(visualCell);
	}

	public Vector3 PlacementToWorldCenter(Vector2I placementCell)
	{
		return Placement.CellToWorldCenter(placementCell);
	}

	public Vector2I PlacementToVisual(Vector2I placementCell)
	{
		return new Vector2I(
			FloorDiv(placementCell.X, Subdivisions),
			FloorDiv(placementCell.Y, Subdivisions));
	}

	public Vector2I VisualToPlacementOrigin(Vector2I visualCell)
	{
		return new Vector2I(visualCell.X * Subdivisions, visualCell.Y * Subdivisions);
	}

	public Vector2I PlacementCellAtVisualLocal(Vector2I visualCell, int localX, int localZ)
	{
		var origin = VisualToPlacementOrigin(visualCell);
		return new Vector2I(origin.X + localX, origin.Y + localZ);
	}

	private Vector2I CellFromWorld(Vector3 worldPosition, float cellSize)
	{
		var half = TerrainSize * 0.5f;
		var x = Mathf.FloorToInt((worldPosition.X + half) / cellSize);
		var z = Mathf.FloorToInt((worldPosition.Z + half) / cellSize);
		return new Vector2I(x, z);
	}

	private static int FloorDiv(int value, int divisor)
	{
		if (value >= 0)
		{
			return value / divisor;
		}

		return -(((-value) + divisor - 1) / divisor);
	}
}
