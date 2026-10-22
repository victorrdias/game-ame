using Godot;
using Godot.Collections;

namespace Vilanova;

[GlobalClass]
public partial class BuildingCatalog : Resource
{
	[Export] public Array<BuildingDefinition> Items { get; set; } = new();
}
