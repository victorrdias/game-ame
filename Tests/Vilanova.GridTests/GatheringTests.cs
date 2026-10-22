using Vilanova;
using Xunit;

namespace Vilanova.GridTests;

public class GatheringTests
{
	[Fact]
	public void TreeFallsOnTheFourthHit()
	{
		var tree = new HarvestProgress(4);
		Assert.True(tree.TryHit(out var fell));
		Assert.Equal(1, tree.Hits);
		Assert.False(fell);
		Assert.True(tree.TryHit(out _));
		Assert.True(tree.TryHit(out _));
		Assert.Equal(3, tree.Hits);
		Assert.False(tree.IsDepleted);
		Assert.True(tree.TryHit(out fell));
		Assert.True(fell);
		Assert.True(tree.IsDepleted);
		Assert.False(tree.TryHit(out _));
		Assert.Equal(4, tree.Hits);
	}

	[Fact]
	public void TwoTreesKeepSeparateProgress()
	{
		var first = new HarvestProgress(4);
		var second = new HarvestProgress(4);
		first.TryHit(out _);
		first.TryHit(out _);
		second.TryHit(out _);
		Assert.Equal(2, first.Hits);
		Assert.Equal(1, second.Hits);
		Assert.False(first.IsDepleted);
		Assert.False(second.IsDepleted);
	}

	[Fact]
	public void RockFallsOnTheFourthHit()
	{
		var rock = new HarvestProgress(4);
		for (var i = 0; i < 3; i++)
		{
			Assert.True(rock.TryHit(out var fell));
			Assert.False(fell);
		}

		Assert.True(rock.TryHit(out var last));
		Assert.True(last);
	}

	[Fact]
	public void CloserTargetWins()
	{
		Assert.True(InteractionArbiter.IsBetter(1.0f, false, "house", 2.0f, true, "tree"));
		Assert.False(InteractionArbiter.IsBetter(2.2f, true, "tree", 1.0f, false, "house"));
	}

	[Fact]
	public void TiePrefersTheResourceThenTheSmallerId()
	{
		Assert.True(InteractionArbiter.IsBetter(1.02f, true, "tree", 1.0f, false, "house"));
		Assert.False(InteractionArbiter.IsBetter(1.02f, false, "house", 1.0f, true, "tree"));
		Assert.True(InteractionArbiter.IsBetter(1.0f, true, "a", 1.0f, true, "b"));
		Assert.False(InteractionArbiter.IsBetter(1.0f, true, "b", 1.0f, true, "a"));
	}
}
