using Godot;
using Godot.Collections;

namespace Vilanova;

[GlobalClass]
public partial class ItemCatalog : Resource
{
	[Export] public Array<ItemDefinition> Items { get; set; } = new();

	public ItemDefinition Find(string id)
	{
		if (Items == null || string.IsNullOrEmpty(id))
		{
			return null;
		}

		foreach (var item in Items)
		{
			if (item != null && item.Id == id)
			{
				return item;
			}
		}

		return null;
	}
}
