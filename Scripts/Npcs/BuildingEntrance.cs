using Godot;

namespace Vilanova;

public readonly struct BuildingApproach
{
	public string BuildingId { get; init; }
	public Vector3 WorldPosition { get; init; }
	public Vector2I Cell { get; init; }
}

/// <summary>
/// Resolves a door approach from the building's local offset, including the R rotation on the node.
/// </summary>
public static class BuildingEntrance
{
	public const float ResidentClearance = 0.36f;

	public static bool TryResolve(PlacedBuilding building, TownWalkability walk, out BuildingApproach approach)
	{
		approach = default;
		if (building?.Definition == null || building.Visual == null || !GodotObject.IsInstanceValid(building.Visual))
		{
			return false;
		}

		if (!building.Definition.IsVisitable || walk?.Grid == null)
		{
			return false;
		}

		var local = building.Definition.EntranceLocalOffset;
		local.Y = 0.0f;
		if (local.LengthSquared() < 0.04f)
		{
			return false;
		}

		var half = StructureCollisionBuilder.GetCollisionHalfExtents(
			building.Definition,
			building.RotationSteps,
			walk.Grid.CellSize);
		var insideX = Mathf.Abs(local.X) <= half.X + ResidentClearance;
		var insideZ = Mathf.Abs(local.Z) <= half.Y + ResidentClearance;
		if (insideX && insideZ)
		{
			return false;
		}

		var world = building.Visual.ToGlobal(local);
		world.Y = 0.0f;
		var cell = walk.Grid.WorldToCell(world);
		if (!walk.IsWalkable(cell) || Occupies(building, cell) || !IsCardinallyAdjacent(building, cell))
		{
			return false;
		}

		approach = new BuildingApproach
		{
			BuildingId = building.InstanceId,
			WorldPosition = world,
			Cell = cell,
		};
		return true;
	}

	private static bool Occupies(PlacedBuilding building, Vector2I cell)
	{
		foreach (var occupied in building.OccupiedCells())
		{
			if (occupied == cell)
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsCardinallyAdjacent(PlacedBuilding building, Vector2I cell)
	{
		foreach (var occupied in building.OccupiedCells())
		{
			var dx = Mathf.Abs(cell.X - occupied.X);
			var dy = Mathf.Abs(cell.Y - occupied.Y);
			if (dx + dy == 1)
			{
				return true;
			}
		}

		return false;
	}
}
