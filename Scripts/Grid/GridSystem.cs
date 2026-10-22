using System.Collections.Generic;
using Godot;

namespace Vilanova;

/// <summary>
/// World-space grid on the XZ plane. Cell size and terrain extent are fixed in world units.
/// </summary>
public sealed class GridSystem
{
	public float CellSize { get; }
	public float TerrainSize { get; }
	public int CellCount { get; }

	private readonly float _halfExtent;

	public GridSystem(float terrainSize, float cellSize)
	{
		TerrainSize = terrainSize;
		CellSize = cellSize;
		_halfExtent = terrainSize * 0.5f;
		CellCount = Mathf.FloorToInt(terrainSize / cellSize);
	}

	public Vector2I WorldToCell(Vector3 worldPosition)
	{
		var x = Mathf.FloorToInt((worldPosition.X + _halfExtent) / CellSize);
		var z = Mathf.FloorToInt((worldPosition.Z + _halfExtent) / CellSize);
		return new Vector2I(x, z);
	}

	public Vector3 CellToWorldCenter(Vector2I cell)
	{
		var x = -_halfExtent + cell.X * CellSize + CellSize * 0.5f;
		var z = -_halfExtent + cell.Y * CellSize + CellSize * 0.5f;
		return new Vector3(x, 0.0f, z);
	}

	public Vector3 FootprintCenterWorld(Vector2I originCell, Vector2I sizeInCells)
	{
		var x = -_halfExtent + originCell.X * CellSize + sizeInCells.X * CellSize * 0.5f;
		var z = -_halfExtent + originCell.Y * CellSize + sizeInCells.Y * CellSize * 0.5f;
		return new Vector3(x, 0.0f, z);
	}

	public Vector2I OriginCellFromWorldCenter(Vector3 worldPosition, Vector2I sizeInCells)
	{
		var centerCell = WorldToCell(worldPosition);
		return new Vector2I(
			centerCell.X - sizeInCells.X / 2,
			centerCell.Y - sizeInCells.Y / 2);
	}

	public bool IsCellInBounds(Vector2I cell)
	{
		return cell.X >= 0 && cell.Y >= 0 && cell.X < CellCount && cell.Y < CellCount;
	}

	public bool IsFootprintInBounds(Vector2I originCell, Vector2I sizeInCells)
	{
		for (var dx = 0; dx < sizeInCells.X; dx++)
		{
			for (var dz = 0; dz < sizeInCells.Y; dz++)
			{
				if (!IsCellInBounds(new Vector2I(originCell.X + dx, originCell.Y + dz)))
				{
					return false;
				}
			}
		}

		return true;
	}

	public IEnumerable<Vector2I> EnumerateFootprint(Vector2I originCell, Vector2I sizeInCells)
	{
		for (var dx = 0; dx < sizeInCells.X; dx++)
		{
			for (var dz = 0; dz < sizeInCells.Y; dz++)
			{
				yield return new Vector2I(originCell.X + dx, originCell.Y + dz);
			}
		}
	}
}
