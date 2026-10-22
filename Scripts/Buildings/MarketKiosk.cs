using Godot;

namespace Vilanova;

public partial class MarketKiosk : Node3D
{
	public override void _Ready()
	{
		var root = EnsureVisualRoot();
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.06f, 0), new Vector3(2.4f, 0.12f, 2.4f), VillagePalette.StoneFoundation);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.75f, 0), new Vector3(2.1f, 1.35f, 2.1f), VillagePalette.TrimPaint);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 1.55f, 0), new Vector3(2.8f, 0.1f, 2.8f), VillagePalette.RoofTerracotta);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 1.95f, 0), new Vector3(0.18f, 0.9f, 0.18f), VillagePalette.WoodDark);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 2.35f, 0), new Vector3(2.9f, 0.12f, 2.9f), new Color("e8c4a0"), new Vector3(0, 0, 0));
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.85f, 1.02f), new Vector3(1.2f, 0.9f, 0.1f), VillagePalette.WoodWarm);
		ProceduralMeshBuilder.AddBox(root, new Vector3(-0.55f, 1.05f, 1.04f), new Vector3(0.35f, 0.35f, 0.08f), VillagePalette.FlowerAccent);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0.55f, 1.05f, 1.04f), new Vector3(0.35f, 0.35f, 0.08f), VillagePalette.FlowerGreen);
	}

	private Node3D EnsureVisualRoot()
	{
		var visualRoot = GetNodeOrNull<Node3D>(CozyHouse.VisualRootName);
		if (visualRoot == null)
		{
			visualRoot = new Node3D { Name = CozyHouse.VisualRootName };
			AddChild(visualRoot);
		}

		return visualRoot;
	}
}
