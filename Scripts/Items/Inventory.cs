using System;

namespace Vilanova;

public sealed class InventorySlot
{
	public string ItemId { get; set; } = string.Empty;
	public int Quantity { get; set; }

	public bool IsEmpty => Quantity <= 0 || string.IsNullOrEmpty(ItemId);

	public void Clear()
	{
		ItemId = string.Empty;
		Quantity = 0;
	}
}

/// <summary>
/// Slot bag. Stacks fill before empty slots are used. The returned count is what fit;
/// the caller keeps anything left over.
/// </summary>
public sealed class Inventory
{
	public const int DefaultSlotCount = 20;
	public const int DefaultMaxStack = 99;

	private readonly InventorySlot[] _slots;
	private readonly Func<string, int> _maxStackOf;

	public event Action Changed;

	public int SlotCount => _slots.Length;

	public Inventory(int slotCount, Func<string, int> maxStackOf)
	{
		var count = slotCount < 1 ? DefaultSlotCount : slotCount;
		_slots = new InventorySlot[count];
		for (var i = 0; i < count; i++)
		{
			_slots[i] = new InventorySlot();
		}

		_maxStackOf = maxStackOf ?? (_ => DefaultMaxStack);
	}

	public int TryAdd(string itemId, int amount)
	{
		if (string.IsNullOrEmpty(itemId) || amount <= 0)
		{
			return 0;
		}

		var max = MaxStack(itemId);
		var left = amount;
		var added = 0;
		for (var i = 0; i < _slots.Length && left > 0; i++)
		{
			var slot = _slots[i];
			if (slot.ItemId != itemId || slot.Quantity <= 0 || slot.Quantity >= max)
			{
				continue;
			}

			var put = Math.Min(max - slot.Quantity, left);
			slot.Quantity += put;
			left -= put;
			added += put;
		}

		for (var i = 0; i < _slots.Length && left > 0; i++)
		{
			var slot = _slots[i];
			if (!slot.IsEmpty)
			{
				continue;
			}

			var put = Math.Min(max, left);
			slot.ItemId = itemId;
			slot.Quantity = put;
			left -= put;
			added += put;
		}

		if (added > 0)
		{
			Changed?.Invoke();
		}

		return added;
	}

	public int TryRemove(string itemId, int amount)
	{
		if (string.IsNullOrEmpty(itemId) || amount <= 0)
		{
			return 0;
		}

		var left = amount;
		var removed = 0;
		for (var i = _slots.Length - 1; i >= 0 && left > 0; i--)
		{
			var slot = _slots[i];
			if (slot.ItemId != itemId || slot.Quantity <= 0)
			{
				continue;
			}

			var take = Math.Min(slot.Quantity, left);
			slot.Quantity -= take;
			left -= take;
			removed += take;
			if (slot.Quantity <= 0)
			{
				slot.Clear();
			}
		}

		if (removed > 0)
		{
			Changed?.Invoke();
		}

		return removed;
	}

	public int Count(string itemId)
	{
		if (string.IsNullOrEmpty(itemId))
		{
			return 0;
		}

		var total = 0;
		foreach (var slot in _slots)
		{
			if (slot.ItemId == itemId)
			{
				total += slot.Quantity;
			}
		}

		return total;
	}

	public InventorySlot GetSlot(int index)
	{
		if (index < 0 || index >= _slots.Length)
		{
			return null;
		}

		return _slots[index];
	}

	private int MaxStack(string itemId)
	{
		var max = _maxStackOf(itemId);
		return max < 1 ? DefaultMaxStack : max;
	}
}
