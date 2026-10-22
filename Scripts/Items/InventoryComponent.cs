using System;
using Godot;

namespace Vilanova;

/// <summary>
/// Slot inventory. It does not know about movement or the camera.
/// </summary>
public partial class InventoryComponent : Node
{
	public const string NodeName = "Inventory";

	[Export] public int SlotCount { get; set; } = Inventory.DefaultSlotCount;
	[Export] public ItemCatalog Catalog { get; set; }

	private Inventory _inventory;

	public event Action InventoryChanged;

	public int Capacity => Ensure().SlotCount;

	public override void _Ready()
	{
		Ensure();
	}

	public int TryAddItem(string itemId, int amount)
	{
		return Ensure().TryAdd(itemId, amount);
	}

	public int TryRemoveItem(string itemId, int amount)
	{
		return Ensure().TryRemove(itemId, amount);
	}

	public int Count(string itemId)
	{
		return Ensure().Count(itemId);
	}

	public InventorySlot GetSlot(int index)
	{
		return Ensure().GetSlot(index);
	}

	public ItemDefinition FindDefinition(string itemId)
	{
		return Catalog?.Find(itemId);
	}

	public string DisplayNameOf(string itemId)
	{
		var definition = FindDefinition(itemId);
		if (definition == null || string.IsNullOrEmpty(definition.DisplayName))
		{
			return itemId ?? string.Empty;
		}

		return definition.DisplayName;
	}

	private Inventory Ensure()
	{
		if (_inventory != null)
		{
			return _inventory;
		}

		_inventory = new Inventory(SlotCount, MaxStackOf);
		_inventory.Changed += () => InventoryChanged?.Invoke();
		return _inventory;
	}

	private int MaxStackOf(string itemId)
	{
		var definition = Catalog?.Find(itemId);
		if (definition == null || definition.MaxStack < 1)
		{
			return Inventory.DefaultMaxStack;
		}

		return definition.MaxStack;
	}
}
