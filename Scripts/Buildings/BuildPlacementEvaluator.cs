using System.Collections.Generic;
using Godot;

namespace Vilanova;

public static class BuildPlacementEvaluator
{
	public static PlacementBlockReason EvaluatePlacement(
		BuildingDefinition definition,
		Vector2I originCell,
		int rotationSteps,
		GridSystem grid,
		BuildingOccupancyGrid occupancy,
		Vector3 playerWorldPosition,
		float maxBuildReach,
		Aabb playerOccupancyBounds,
		IReadOnlyList<Aabb> residentBounds)
	{
		var rotatedSize = FootprintHelper.GetRotatedSize(definition.SizeInCells, rotationSteps);
		var footprintCenter = grid.FootprintCenterWorld(originCell, rotatedSize);

		if (HorizontalReach.DistanceXZ(playerWorldPosition, footprintCenter) > maxBuildReach)
		{
			return PlacementBlockReason.TooFar;
		}

		var outOfTerrain = false;
		var occupied = false;
		foreach (var cell in FootprintHelper.EnumerateFootprint(originCell, definition.SizeInCells, rotationSteps))
		{
			if (!grid.IsCellInBounds(cell))
			{
				outOfTerrain = true;
				break;
			}

			if (occupancy.GetAtCell(cell) != null)
			{
				occupied = true;
				break;
			}
		}

		if (outOfTerrain)
		{
			return PlacementBlockReason.OutOfTerrain;
		}

		if (occupied)
		{
			return PlacementBlockReason.Occupied;
		}

		if (FootprintOverlapsBounds(originCell, definition, rotationSteps, grid, playerOccupancyBounds))
		{
			return PlacementBlockReason.OverlapsPlayer;
		}

		if (residentBounds != null)
		{
			foreach (var bounds in residentBounds)
			{
				if (FootprintOverlapsBounds(originCell, definition, rotationSteps, grid, bounds))
				{
					return PlacementBlockReason.OverlapsResident;
				}
			}
		}

		return PlacementBlockReason.None;
	}

	public static bool IsWithinBuildReach(Vector3 playerWorldPosition, Vector3 targetWorldPosition, float maxBuildReach)
	{
		return HorizontalReach.DistanceXZ(playerWorldPosition, targetWorldPosition) <= maxBuildReach;
	}

	public static string DescribeReason(PlacementBlockReason reason)
	{
		return reason switch
		{
			PlacementBlockReason.Occupied => "Área ocupada.",
			PlacementBlockReason.OutOfTerrain => "Fora do terreno.",
			PlacementBlockReason.TooFar => "Muito distante do personagem.",
			PlacementBlockReason.OverlapsPlayer => "Colide com o personagem.",
			PlacementBlockReason.OverlapsResident => "Um morador está neste local.",
			_ => string.Empty,
		};
	}

	private static bool FootprintOverlapsBounds(
		Vector2I originCell,
		BuildingDefinition definition,
		int rotationSteps,
		GridSystem grid,
		Aabb bounds)
	{
		if (bounds.Size == Vector3.Zero)
		{
			return false;
		}

		var cellSize = grid.CellSize;
		foreach (var cell in FootprintHelper.EnumerateFootprint(originCell, definition.SizeInCells, rotationSteps))
		{
			var center = grid.CellToWorldCenter(cell);
			var cellBounds = new Aabb(
				center - new Vector3(cellSize * 0.5f, 0.0f, cellSize * 0.5f),
				new Vector3(cellSize, 2.5f, cellSize));
			if (bounds.Intersects(cellBounds))
			{
				return true;
			}
		}

		return false;
	}
}
