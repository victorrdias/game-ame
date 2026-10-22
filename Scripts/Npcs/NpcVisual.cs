using Godot;

namespace Vilanova;

public partial class NpcVisual : Node3D
{
	public Color ShirtColor { get; set; } = VillagePalette.WallCream;
	public Color PantsColor { get; set; } = VillagePalette.DoorWood;
	public Color SkinColor { get; set; } = new("f5d5b8");

	public override void _Ready()
	{
		var packed = GD.Load<PackedScene>("res://Assets/Models/vn_player.glb");
		if (packed == null)
		{
			PlayerVisualBuilder.Build(this, ShirtColor, PantsColor, SkinColor);
			AddChild(new NpcActivityVisual { Name = "Activity" });
			return;
		}

		var model = packed.Instantiate<Node3D>();
		model.Name = "BlenderModel";
		model.RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f);
		AddChild(model);
		StructureCollisionBuilder.DisableCollisionRecursively(model);
		TintClothes(model);
		AddChild(new PlayerWalkAnimation
		{
			Name = "Walk",
			StartPhase = (float)GD.RandRange(0.0, Mathf.Tau),
		});
		AddChild(new NpcActivityVisual { Name = "Activity" });
	}

	private void TintClothes(Node node)
	{
		if (node is MeshInstance3D mesh && mesh.Mesh != null)
		{
			for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
			{
				var source = mesh.GetActiveMaterial(surface) ?? mesh.Mesh.SurfaceGetMaterial(surface);
				if (source?.Duplicate() is not BaseMaterial3D copy)
				{
					continue;
				}

				var materialName = source.ResourceName.ToLowerInvariant();
				if (materialName.Contains("tunic"))
				{
					copy.AlbedoColor = ShirtColor;
				}
				else if (materialName.Contains("pants"))
				{
					copy.AlbedoColor = PantsColor;
				}
				else if (materialName.Contains("skin"))
				{
					copy.AlbedoColor = SkinColor;
				}
				else
				{
					continue;
				}

				mesh.SetSurfaceOverrideMaterial(surface, copy);
			}
		}

		foreach (var child in node.GetChildren())
		{
			TintClothes(child);
		}
	}
}
