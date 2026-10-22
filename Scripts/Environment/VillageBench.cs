using Godot;

namespace Vilanova;

public partial class VillageBench : Node3D
{
	public override void _Ready()
	{
		var root = EnsureVisualRoot();
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.22f, 0), new Vector3(1.4f, 0.12f, 0.45f), VillagePalette.WoodWarm);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.42f, 0), new Vector3(1.35f, 0.08f, 0.4f), VillagePalette.WoodDark);
		ProceduralMeshBuilder.AddBox(root, new Vector3(-0.55f, 0.18f, 0), new Vector3(0.1f, 0.35f, 0.35f), VillagePalette.WoodDark);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0.55f, 0.18f, 0), new Vector3(0.1f, 0.35f, 0.35f), VillagePalette.WoodDark);
	}

	private Node3D EnsureVisualRoot()
	{
		var visualRoot = GetNodeOrNull<Node3D>(CozyTree.VisualRootName);
		if (visualRoot == null)
		{
			visualRoot = new Node3D { Name = CozyTree.VisualRootName };
			AddChild(visualRoot);
		}

		return visualRoot;
	}
}
