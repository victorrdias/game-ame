using Godot;

namespace Vilanova;

public static class BenchInteractionBuilder
{
	private static readonly string[] BenchIds = { "rustic_bench", "village_bench", "stone_bench", "plaza_bench" };

	public static bool IsBench(BuildingDefinition definition)
	{
		if (definition == null)
		{
			return false;
		}

		foreach (var id in BenchIds)
		{
			if (definition.Id == id)
			{
				return true;
			}
		}

		return false;
	}

	public static void RegisterBenchPoints(
		PlacedBuilding building,
		TownWalkability walk,
		NpcInteractionRegistry registry)
	{
		if (!IsBench(building?.Definition) || building.Visual == null || walk == null || registry == null)
		{
			return;
		}

		if (!TryBuildSeat(building, walk, out var approachCell, out var approachWorld, out var seatWorld, out var seatYaw))
		{
			return;
		}

		var id = registry.NewPointId("bench");
		var point = new NpcInteractionPoint(
			id,
			NpcActivityType.SitBench,
			approachWorld,
			approachCell,
			seatWorld,
			seatYaw,
			building.Visual,
			building.InstanceId,
			() => IsBenchStillValid(building));
		registry.Register(point);
	}

	private static bool IsBenchStillValid(PlacedBuilding building)
	{
		return building?.Definition != null &&
		       building.Visual != null &&
		       GodotObject.IsInstanceValid(building.Visual) &&
		       IsBench(building.Definition);
	}

	private static bool TryBuildSeat(
		PlacedBuilding building,
		TownWalkability walk,
		out Vector2I approachCell,
		out Vector3 approachWorld,
		out Vector3 seatWorld,
		out float seatYaw)
	{
		approachCell = default;
		approachWorld = Vector3.Zero;
		seatWorld = Vector3.Zero;
		seatYaw = 0.0f;

		var localSeat = building.Definition.Id == "village_bench"
			? Vector3.Zero
			: new Vector3(-0.1f, 0.0f, 0.0f);
		seatWorld = walk.StandPoint(building.Visual.ToGlobal(localSeat));

		if (!TryFindApproach(building, walk, seatWorld, out approachCell, out approachWorld, out var approachOut))
		{
			return false;
		}

		var forward = SeatForward(building);
		if (building.Definition.Id == "village_bench" && approachOut.Dot(forward) < 0.0f)
		{
			forward = -forward;
		}

		seatYaw = Mathf.Atan2(forward.X, forward.Z);
		return true;
	}

	/// <summary>
	/// Direction the sitter looks. Modeled benches keep the backrest on local +X, so the open side is -X.
	/// The root yaw from placement is included, so rotating the bench turns the seat with it.
	/// </summary>
	public static Vector3 SeatForward(PlacedBuilding building)
	{
		var basis = building.Visual.GlobalBasis;
		var facing = building.Definition.Id == "village_bench" ? basis.Z : -basis.X;
		facing.Y = 0.0f;
		return facing.LengthSquared() > 0.0001f ? facing.Normalized() : Vector3.Forward;
	}

	private static bool TryFindApproach(
		PlacedBuilding building,
		TownWalkability walk,
		Vector3 seatWorld,
		out Vector2I approachCell,
		out Vector3 approachWorld,
		out Vector3 faceDirection)
	{
		approachCell = default;
		approachWorld = Vector3.Zero;
		faceDirection = Vector3.Back;

		var seatCell = walk.Grid.WorldToCell(seatWorld);
		var look = SeatForward(building);
		var bestScore = float.MinValue;
		var found = false;

		foreach (var offset in TownWalkability.CardinalOffsets)
		{
			var cell = new Vector2I(seatCell.X + offset.X, seatCell.Y + offset.Y);
			if (!walk.IsWalkable(cell) || BenchOccupiesVisual(building, cell, walk.Layout))
			{
				continue;
			}

			var world = walk.StandPoint(walk.Grid.CellToWorldCenter(cell));
			var fromSeat = world - seatWorld;
			fromSeat.Y = 0.0f;
			if (fromSeat.LengthSquared() < 0.01f)
			{
				continue;
			}

			var outward = fromSeat.Normalized();
			var alongFront = outward.Dot(look);
			var score = building.Definition.Id == "village_bench"
				? Mathf.Abs(alongFront) * 4.0f
				: alongFront * 4.0f;
			score -= fromSeat.Length() * 0.05f;
			if (score > bestScore)
			{
				bestScore = score;
				approachCell = cell;
				approachWorld = world;
				faceDirection = outward;
				found = true;
			}
		}

		return found;
	}

	private static bool BenchOccupiesVisual(PlacedBuilding building, Vector2I visualCell, TownGridLayout layout)
	{
		foreach (var occupied in building.OccupiedCells())
		{
			if (layout.PlacementToVisual(occupied) == visualCell)
			{
				return true;
			}
		}

		return false;
	}
}
