using Godot;

namespace Vilanova;

public partial class PlayerVisual : Node3D
{
	public override void _Ready()
	{
		PlayerVisualBuilder.Build(this);
	}
}
