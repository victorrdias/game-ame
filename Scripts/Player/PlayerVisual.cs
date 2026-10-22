using Godot;

namespace Vilanova;

public partial class PlayerVisual : Node3D
{
	public override void _Ready()
	{
		var packed = GD.Load<PackedScene>("res://Assets/Models/vn_player.glb");
		if (packed == null)
		{
			PlayerVisualBuilder.Build(this);
			AddChild(new NpcActivityVisual { Name = "Activity" });
			return;
		}

		var model = packed.Instantiate<Node3D>();
		model.Name = "BlenderModel";
		model.RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f);
		AddChild(model);
		StructureCollisionBuilder.DisableCollisionRecursively(model);
		var walk = new PlayerWalkAnimation { Name = "Walk" };
		AddChild(walk);
		AddChild(new NpcActivityVisual { Name = "Activity" });
	}
}
