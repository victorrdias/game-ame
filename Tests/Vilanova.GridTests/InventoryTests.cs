using Vilanova;
using Xunit;

namespace Vilanova.GridTests;

public class InventoryTests
{
	[Fact]
	public void StacksUpToNinetyNineThenUsesTheNextSlot()
	{
		var bag = new Inventory(20, _ => 99);
		Assert.Equal(99, bag.TryAdd(ItemIds.Wood, 99));
		Assert.Equal(1, bag.TryAdd(ItemIds.Wood, 1));
		Assert.Equal(99, bag.GetSlot(0).Quantity);
		Assert.Equal(ItemIds.Wood, bag.GetSlot(1).ItemId);
		Assert.Equal(1, bag.GetSlot(1).Quantity);
		Assert.Equal(100, bag.Count(ItemIds.Wood));
	}

	[Fact]
	public void FullBagKeepsTheRemainderOut()
	{
		var bag = new Inventory(2, _ => 10);
		Assert.Equal(20, bag.TryAdd(ItemIds.Stone, 25));
		Assert.Equal(20, bag.Count(ItemIds.Stone));
		Assert.Equal(0, bag.TryAdd(ItemIds.Stone, 4));
		Assert.Equal(20, bag.Count(ItemIds.Stone));
		Assert.Equal(0, bag.TryAdd(ItemIds.Wood, 1));
	}

	[Fact]
	public void PartialAddFillsTheLastRoom()
	{
		var bag = new Inventory(1, _ => 10);
		Assert.Equal(4, bag.TryAdd(ItemIds.Wood, 4));
		Assert.Equal(6, bag.TryAdd(ItemIds.Wood, 9));
		Assert.Equal(10, bag.Count(ItemIds.Wood));
	}

	[Fact]
	public void WoodAndStoneDoNotShareAStack()
	{
		var bag = new Inventory(4, _ => 99);
		bag.TryAdd(ItemIds.Wood, 3);
		bag.TryAdd(ItemIds.Stone, 2);
		Assert.Equal(ItemIds.Wood, bag.GetSlot(0).ItemId);
		Assert.Equal(ItemIds.Stone, bag.GetSlot(1).ItemId);
		Assert.Equal(3, bag.Count(ItemIds.Wood));
		Assert.Equal(2, bag.Count(ItemIds.Stone));
	}

	[Fact]
	public void RemoveClearsAnEmptyStack()
	{
		var bag = new Inventory(2, _ => 99);
		bag.TryAdd(ItemIds.Wood, 5);
		Assert.Equal(2, bag.TryRemove(ItemIds.Wood, 2));
		Assert.Equal(3, bag.Count(ItemIds.Wood));
		Assert.Equal(3, bag.TryRemove(ItemIds.Wood, 10));
		Assert.True(bag.GetSlot(0).IsEmpty);
	}

	[Fact]
	public void TakeDoesNotCollectTwiceOrDropTheRest()
	{
		var full = new WorldDropStack { ItemId = ItemIds.Wood, Quantity = 1 };
		Assert.Equal(1, full.Take(1));
		Assert.Equal(WorldDropState.Collected, full.State);
		Assert.Equal(0, full.Take(1));

		var partial = new WorldDropStack { ItemId = ItemIds.Stone, Quantity = 5 };
		Assert.Equal(2, partial.Take(2));
		Assert.Equal(3, partial.Quantity);
		Assert.Equal(WorldDropState.Available, partial.State);
		Assert.True(partial.Yielded);
		Assert.Equal(0, partial.Take(0));
		Assert.Equal(3, partial.Quantity);
	}

	[Fact]
	public void MagnetRespectsRadiusDelayHeightAndWallYield()
	{
		Assert.True(PickupRules.CanAttract(2.0f, 0.1f, true, 0.5, 0.45, 2.0f, 1.75f, false));
		Assert.False(PickupRules.CanAttract(2.2f, 0.1f, true, 0.5, 0.45, 2.0f, 1.75f, false));
		Assert.False(PickupRules.CanAttract(1.0f, 0.1f, false, 0.5, 0.45, 2.0f, 1.75f, false));
		Assert.False(PickupRules.CanAttract(1.0f, 0.1f, true, 0.2, 0.45, 2.0f, 1.75f, false));
		Assert.False(PickupRules.CanAttract(1.0f, 4.0f, true, 0.5, 0.45, 2.0f, 1.75f, false));
		Assert.False(PickupRules.CanAttract(0.2f, 0.1f, true, 0.5, 0.45, 2.0f, 1.75f, true));
		Assert.True(PickupRules.CanCollect(0.4f, 0.4f));
		Assert.False(PickupRules.CanCollect(0.5f, 0.4f));
	}
}
