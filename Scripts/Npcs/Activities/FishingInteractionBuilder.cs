using System.Collections.Generic;
using Godot;

namespace Vilanova;

public static class FishingInteractionBuilder
{
	public static void RegisterRiverPoints(
		PlacedBuilding river,
		TownWalkability walk,
		NpcInteractionRegistry registry,
		HashSet<string> dedupeKeys)
	{
		if (river?.Definition == null || river.Definition.LinkGroup != "river" || walk == null || registry == null)
		{
			return;
		}

		if (river.Visual == null || !GodotObject.IsInstanceValid(river.Visual))
		{
			return;
		}

		var visualCells = new HashSet<Vector2I>();
		foreach (var placementCell in river.OccupiedCells())
		{
			visualCells.Add(walk.Layout.PlacementToVisual(placementCell));
		}

		foreach (var waterCell in visualCells)
		{
			foreach (var offset in TownWalkability.CardinalOffsets)
			{
				var shoreCell = new Vector2I(waterCell.X + offset.X, waterCell.Y + offset.Y);
				if (!walk.IsWalkable(shoreCell) || visualCells.Contains(shoreCell))
				{
					continue;
				}

				var key = $"{shoreCell.X},{shoreCell.Y}:{offset.X},{offset.Y}";
				if (!dedupeKeys.Add(key))
				{
					continue;
				}

				var shoreWorld = walk.StandPoint(walk.Grid.CellToWorldCenter(shoreCell));
				var waterWorld = walk.StandPoint(walk.Grid.CellToWorldCenter(waterCell));
				var face = waterWorld - shoreWorld;
				face.Y = 0.0f;
				if (face.LengthSquared() < 0.04f)
				{
					continue;
				}

				face = face.Normalized();
				var yaw = Mathf.Atan2(face.X, face.Z);
				var id = registry.NewPointId("fish");
				var capturedRiver = river;
				var capturedShore = shoreCell;
				var capturedWater = waterCell;
				var point = new NpcInteractionPoint(
					id,
					NpcActivityType.Fish,
					shoreWorld,
					shoreCell,
					shoreWorld,
					yaw,
					river.Visual,
					river.InstanceId,
					() => IsShoreStillValid(capturedRiver, walk, capturedShore, capturedWater));
				registry.Register(point);
			}
		}
	}

	private static bool IsShoreStillValid(
		PlacedBuilding river,
		TownWalkability walk,
		Vector2I shoreCell,
		Vector2I _)
	{
		if (river?.Definition == null || river.Definition.LinkGroup != "river")
		{
			return false;
		}

		if (river.Visual == null || !GodotObject.IsInstanceValid(river.Visual))
		{
			return false;
		}

		return walk.IsWalkable(shoreCell);
	}
}
