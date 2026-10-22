using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;

namespace Vilanova.GridTests;

public class TerrainHeightTests
{
	private readonly TerrainHeightField _field = TerrainHeightField.CreateValley();
	private readonly TownGridLayout _layout = new(TerrainHeightField.ValleySize, 2.0f);

	[Fact]
	public void WorldCellIgnoresHeight()
	{
		var floor = new Vector3(2.2f, 0.0f, -4.4f);
		var raised = new Vector3(floor.X, 2.0f, floor.Z);
		Assert.Equal(_layout.WorldToVisual(floor), _layout.WorldToVisual(raised));
		Assert.Equal(_layout.WorldToPlacement(floor), _layout.WorldToPlacement(raised));
	}

	[Fact]
	public void ValleyIsSixTimesLargerAndHasHighsAndLows()
	{
		Assert.Equal(240.0f, _field.TerrainSize);
		Assert.Equal(120, _field.CellCount);
		Assert.Equal(0, _field.LevelAt(new Vector2I(60, 60)));
		Assert.Equal(TerrainCellKind.Plateau, _field.KindAt(new Vector2I(60, 60)));
		Assert.Equal(-1, _field.LevelAt(new Vector2I(62, 24)));
		Assert.Equal(1, _field.LevelAt(new Vector2I(60, 88)));
		Assert.Equal(2, _field.LevelAt(new Vector2I(60, 100)));
		Assert.Equal(3, _field.LevelAt(new Vector2I(60, 104)));
		Assert.Equal(-0.5f, _field.SampleY(_field.CellCenter(new Vector2I(62, 35)).X, _field.CellCenter(new Vector2I(62, 35)).Z), 2);
	}

	[Fact]
	public void ExistingPropsStayOnTheValleyFloor()
	{
		AssertFloor(new Vector3(-1.0f, 0.0f, -1.0f), new Vector2I(9, 9));
		AssertFloor(new Vector3(-8.0f, 0.0f, -2.0f), new Vector2I(6, 6));
		AssertFloor(new Vector3(0.0f, 0.0f, -6.0f), new Vector2I(6, 6));
		AssertFloor(new Vector3(-5.0f, 0.0f, -1.0f), Vector2I.One);
		AssertFloor(new Vector3(5.0f, 0.0f, -3.0f), Vector2I.One);
		AssertFloor(new Vector3(-3.0f, 0.0f, -9.0f), Vector2I.One);
		AssertFloor(new Vector3(-7.0f, 0.0f, 5.0f), Vector2I.One);
		AssertFloor(new Vector3(-9.0f, 0.0f, -5.0f), Vector2I.One);
		AssertFloor(new Vector3(4.0f, 0.0f, 4.0f), Vector2I.One);
	}

	[Fact]
	public void FootprintsAcceptFlatLevelsAndRejectSlopes()
	{
		Assert.Equal(TerrainFootprintStatus.Ok, Footprint(new Vector2I(180, 180), new Vector2I(6, 6), out var floor));
		Assert.Equal(0.0f, floor);
		Assert.Equal(TerrainFootprintStatus.Ok, Footprint(new Vector2I(180, 264), new Vector2I(6, 6), out var shelf));
		Assert.Equal(1.0f, shelf);
		Assert.Equal(TerrainFootprintStatus.Ok, Footprint(new Vector2I(210, 300), new Vector2I(6, 6), out var lookout));
		Assert.Equal(2.0f, lookout);
		Assert.Equal(TerrainFootprintStatus.Uneven, Footprint(new Vector2I(174, 243), Vector2I.One, out _));
		Assert.Equal(TerrainFootprintStatus.OutOfPlay, Footprint(new Vector2I(0, 120), Vector2I.One, out _));
	}

	[Fact]
	public void NavigationClimbsOnlyThroughRamps()
	{
		var walk = new TownWalkability(_layout, new BuildingOccupancyGrid(), _field);
		Assert.False(walk.IsWalkable(new Vector2I(0, 40)));
		Assert.False(walk.CanTraverse(new Vector2I(70, 81), new Vector2I(70, 82)));
		Assert.True(walk.CanTraverse(new Vector2I(60, 80), new Vector2I(60, 81)));
		Assert.True(walk.CanTraverse(new Vector2I(60, 81), new Vector2I(60, 82)));

		var path = new List<Vector2I>();
		Assert.True(NpcGridPathfinder.TryFindPath(
			new Vector2I(60, 60),
			new Vector2I(60, 104),
			walk.IsWalkable,
			path,
			walk.CanTraverse));
		Assert.Contains(path, cell => _field.LevelAt(cell) == 3);
		AssertRampSegments(walk, path);

		var cliffPath = new List<Vector2I>();
		Assert.True(NpcGridPathfinder.TryFindPath(
			new Vector2I(70, 70),
			new Vector2I(70, 88),
			walk.IsWalkable,
			cliffPath,
			walk.CanTraverse));
		AssertRampSegments(walk, cliffPath);
		Assert.DoesNotContain(
			Enumerable.Range(1, cliffPath.Count - 1),
			i => cliffPath[i - 1] == new Vector2I(70, 81) && cliffPath[i] == new Vector2I(70, 82));
	}

	private void AssertRampSegments(TownWalkability walk, List<Vector2I> path)
	{
		for (var i = 1; i < path.Count; i++)
		{
			var from = path[i - 1];
			var to = path[i];
			var stepX = System.Math.Sign(to.X - from.X);
			var stepZ = System.Math.Sign(to.Y - from.Y);
			var cursor = from;
			while (cursor != to)
			{
				var next = new Vector2I(cursor.X + stepX, cursor.Y + stepZ);
				Assert.True(walk.CanTraverse(cursor, next), $"degrau inválido {cursor} -> {next}");
				cursor = next;
			}
		}
	}

	private TerrainFootprintStatus Footprint(Vector2I origin, Vector2I size, out float height)
	{
		return _field.EvaluateFootprint(
			FootprintHelper.EnumerateFootprint(origin, size, 0),
			_layout.PlacementToVisual,
			out height);
	}

	private void AssertFloor(Vector3 world, Vector2I size)
	{
		var origin = _layout.Placement.OriginCellFromWorldCenter(world, size);
		var visuals = FootprintHelper.EnumerateFootprint(origin, size, 0)
			.Select(cell => _layout.PlacementToVisual(cell))
			.Distinct();
		foreach (var visual in visuals)
		{
			Assert.True(_field.InBounds(visual), $"prop cell {visual} from {world}");
			Assert.Equal(TerrainCellKind.Plateau, _field.KindAt(visual));
			Assert.Equal(0, _field.LevelAt(visual));
		}
	}
}
