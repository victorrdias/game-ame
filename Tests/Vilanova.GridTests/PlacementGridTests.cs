using System.Collections.Generic;
using System.Linq;
using Godot;
using Vilanova;
using Xunit;

namespace Vilanova.GridTests;

public class PlacementGridTests
{
	private readonly TownGridLayout _layout = new(40.0f, 2.0f);

	[Fact]
	public void VisualCellContainsNinePlacementCells()
	{
		var visual = new Vector2I(4, 7);
		var origin = _layout.VisualToPlacementOrigin(visual);
		Assert.Equal(visual.X * 3, origin.X);
		Assert.Equal(visual.Y * 3, origin.Y);

		for (var x = 0; x < 3; x++)
		{
			for (var z = 0; z < 3; z++)
			{
				var subtile = _layout.PlacementCellAtVisualLocal(visual, x, z);
				Assert.Equal(visual, _layout.PlacementToVisual(subtile));
			}
		}
	}

	[Fact]
	public void WorldConversionsShareOrigin()
	{
		var world = new Vector3(1.2f, 0.0f, -3.4f);
		var visual = _layout.WorldToVisual(world);
		var placement = _layout.WorldToPlacement(world);
		Assert.Equal(visual, _layout.PlacementToVisual(placement));
		Assert.Equal(_layout.Visual.WorldToCell(world), visual);
		Assert.Equal(_layout.Placement.WorldToCell(world), placement);
	}

	[Fact]
	public void CameraYawDoesNotChangeLogicalCell()
	{
		var world = new Vector3(3.1f, 0.0f, 4.2f);
		var before = _layout.WorldToPlacement(world);
		var yaw = Mathf.DegToRad(37.0f);
		var rotatedDirection = new Vector3(Mathf.Cos(yaw), 0.0f, Mathf.Sin(yaw));
		Assert.True(rotatedDirection.LengthSquared() > 0.5f);
		Assert.Equal(before, _layout.WorldToPlacement(world));
	}

	[Theory]
	[InlineData(1, 1, 1)]
	[InlineData(3, 1, 3)]
	[InlineData(3, 3, 9)]
	[InlineData(6, 6, 36)]
	public void FootprintOccupiesExpectedSubtileCount(int sizeX, int sizeZ, int expected)
	{
		var cells = FootprintHelper.EnumerateFootprint(new Vector2I(12, 12), new Vector2I(sizeX, sizeZ), 0);
		Assert.Equal(expected, cells.Count());
	}

	[Fact]
	public void BenchRotationSwapsThreeByOne()
	{
		var cells = FootprintHelper.EnumerateFootprint(new Vector2I(10, 10), new Vector2I(3, 1), 1).ToList();
		Assert.Equal(3, cells.Count);
		Assert.Equal(new Vector2I(1, 3), FootprintHelper.GetRotatedSize(new Vector2I(3, 1), 1));
		Assert.Contains(new Vector2I(10, 10), cells);
		Assert.Contains(new Vector2I(10, 12), cells);
	}
}
