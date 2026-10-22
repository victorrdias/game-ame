using Godot;

namespace Vilanova;

public enum ItemCategory
{
	Material,
}

[GlobalClass]
public partial class ItemDefinition : Resource
{
	[Export] public string Id { get; set; } = string.Empty;
	[Export] public string DisplayName { get; set; } = string.Empty;
	[Export] public string Description { get; set; } = string.Empty;
	[Export] public Texture2D Icon { get; set; }
	[Export] public int MaxStack { get; set; } = 99;
	[Export] public ItemCategory Category { get; set; } = ItemCategory.Material;
}
