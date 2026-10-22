using Godot;

namespace Vilanova;

public partial class SmallShop : Node3D
{
	public override void _Ready()
	{
		var root = EnsureVisualRoot();
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.08f, 0), new Vector3(3.4f, 0.16f, 3.0f), VillagePalette.StoneFoundation);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 1.15f, 0), new Vector3(3.1f, 2.1f, 2.85f), VillagePalette.WallCream);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 2.25f, 0), new Vector3(3.45f, 0.12f, 3.15f), VillagePalette.RoofTerracotta);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 2.55f, 0.95f), new Vector3(3.5f, 0.12f, 2.2f), VillagePalette.RoofShadow, new Vector3(-34, 0, 0));
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 2.55f, -0.95f), new Vector3(3.5f, 0.12f, 2.2f), VillagePalette.RoofTerracotta, new Vector3(34, 0, 0));
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 1.05f, 1.48f), new Vector3(1.8f, 1.2f, 0.12f), VillagePalette.WindowGlass, roughness: 0.35f);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 1.05f, 1.52f), new Vector3(1.95f, 1.35f, 0.08f), VillagePalette.WindowFrame);
		ProceduralMeshBuilder.AddBox(root, new Vector3(-1.0f, 0.95f, 1.5f), new Vector3(0.75f, 1.55f, 0.1f), VillagePalette.DoorWood);
		ProceduralMeshBuilder.AddBox(root, new Vector3(1.15f, 2.0f, 0), new Vector3(0.35f, 0.85f, 0.35f), VillagePalette.ChimneyBrick);
		ProceduralMeshBuilder.AddBox(root, new Vector3(-1.25f, 1.55f, 1.52f), new Vector3(0.55f, 0.45f, 0.08f), VillagePalette.WoodWarm);
	}

	private Node3D EnsureVisualRoot()
	{
		var visualRoot = GetNodeOrNull<Node3D>(CozyHouse.VisualRootName);
		if (visualRoot != null)
		{
			return visualRoot;
		}

		visualRoot = new Node3D { Name = CozyHouse.VisualRootName };
		AddChild(visualRoot);
		return visualRoot;
	}
}
