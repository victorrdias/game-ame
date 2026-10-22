using Godot;

namespace Vilanova;

public partial class NpcVisual : Node3D
{
	public Color ShirtColor { get; set; } = VillagePalette.WallCream;
	public Color PantsColor { get; set; } = VillagePalette.DoorWood;
	public Color SkinColor { get; set; } = new("f5d5b8");

	public override void _Ready()
	{
		PlayerVisualBuilder.Build(this, ShirtColor, PantsColor, SkinColor);
	}
}
