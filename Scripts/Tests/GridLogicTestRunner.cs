using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Vilanova;

/// <summary>
/// Headless checks that need Godot Resources. Run the scene, do not set it as main.
/// </summary>
public partial class GridLogicTestRunner : Node
{
	public override void _Ready()
	{
		var failures = new List<string>();
		CheckSharedVisualCell(failures);
		CheckDemolishReleasesExactSubtiles(failures);
		CheckNavigationUpdatesAfterRemove(failures);

		if (failures.Count == 0)
		{
			GD.Print("GRID_TESTS_OK");
			GetTree().Quit(0);
			return;
		}

		foreach (var failure in failures)
		{
			GD.PrintErr(failure);
		}

		GetTree().Quit(1);
	}

	private static void CheckSharedVisualCell(List<string> failures)
	{
		var layout = new TownGridLayout(40.0f, 2.0f);
		var visual = new Vector2I(8, 8);
		var postCell = layout.PlacementCellAtVisualLocal(visual, 0, 0);
		var benchOrigin = layout.PlacementCellAtVisualLocal(visual, 0, 1);
		var occupancy = new BuildingOccupancyGrid();
		var post = Place(Definition(Vector2I.One, "post"), postCell);
		var bench = Place(Definition(new Vector2I(3, 1), "bench"), benchOrigin);

		if (!occupancy.CanPlace(post.Definition, post.OriginCell, 0, layout.Placement))
		{
			failures.Add("post should fit an empty visual cell");
		}

		occupancy.Register(post);
		if (!occupancy.CanPlace(bench.Definition, bench.OriginCell, 0, layout.Placement))
		{
			failures.Add("bench should fit beside the post inside the same visual cell");
		}

		occupancy.Register(bench);
		if (layout.PlacementToVisual(postCell) != visual || layout.PlacementToVisual(benchOrigin) != visual)
		{
			failures.Add("post and bench should stay inside one visual cell");
		}
	}

	private static void CheckDemolishReleasesExactSubtiles(List<string> failures)
	{
		var layout = new TownGridLayout(40.0f, 2.0f);
		var occupancy = new BuildingOccupancyGrid();
		var post = Place(Definition(Vector2I.One, "post"), new Vector2I(4, 4));
		var bench = Place(Definition(new Vector2I(3, 1), "bench"), new Vector2I(4, 5));
		occupancy.Register(post);
		occupancy.Register(bench);
		occupancy.Unregister(post);

		if (occupancy.GetAtCell(new Vector2I(4, 4)) != null)
		{
			failures.Add("demolish should free the post subtile");
		}

		if (occupancy.GetAtCell(new Vector2I(4, 5)) != bench || occupancy.GetAtCell(new Vector2I(6, 5)) != bench)
		{
			failures.Add("demolish should keep the bench subtiles");
		}

		if (!occupancy.CanPlace(post.Definition, post.OriginCell, 0, layout.Placement))
		{
			failures.Add("freed post subtile should accept a new post");
		}
	}

	private static void CheckNavigationUpdatesAfterRemove(List<string> failures)
	{
		var layout = new TownGridLayout(40.0f, 2.0f);
		var occupancy = new BuildingOccupancyGrid();
		var walk = new TownWalkability(layout, occupancy);
		var blocked = new Vector2I(5, 5);
		var corner = layout.PlacementCellAtVisualLocal(blocked, 0, 0);
		var cornerPost = Place(Definition(Vector2I.One, "corner"), corner);
		occupancy.Register(cornerPost);
		if (!walk.IsWalkable(blocked))
		{
			failures.Add("a corner post should not seal the 2m navigation cell");
		}

		var centerBlock = Place(Definition(new Vector2I(3, 3), "fill"), layout.VisualToPlacementOrigin(blocked));
		occupancy.Register(centerBlock);
		if (walk.IsWalkable(blocked))
		{
			failures.Add("a full visual cell should block navigation");
		}

		var path = new List<Vector2I>();
		if (!NpcGridPathfinder.TryFindPath(new Vector2I(5, 3), new Vector2I(5, 7), walk.IsWalkable, path) ||
		    path.Contains(blocked) ||
		    path.Count <= 2)
		{
			failures.Add("npc path should route around a blocked visual cell");
		}

		occupancy.Unregister(centerBlock);
		if (!walk.IsWalkable(blocked))
		{
			failures.Add("removing the blocking footprint should reopen navigation");
		}

		if (!NpcGridPathfinder.TryFindPath(new Vector2I(5, 3), new Vector2I(5, 7), walk.IsWalkable, path) ||
		    path.Count != 2)
		{
			failures.Add("npc path should go straight after the obstacle is removed");
		}
	}

	private static BuildingDefinition Definition(Vector2I size, string id)
	{
		return new BuildingDefinition
		{
			Id = id,
			DisplayName = id,
			SizeInCells = size,
			BlocksMovement = true,
			CanDemolish = true,
		};
	}

	private static PlacedBuilding Place(BuildingDefinition definition, Vector2I origin)
	{
		return new PlacedBuilding(null, definition, origin, 0, definition.Id);
	}
}
