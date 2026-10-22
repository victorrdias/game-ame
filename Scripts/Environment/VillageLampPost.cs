using Godot;

namespace Vilanova;

public partial class VillageLampPost : Node3D
{
	public override void _Ready()
	{
		var root = EnsureVisualRoot();
		ProceduralMeshBuilder.AddCylinder(root, new Vector3(0, 0.55f, 0), 0.06f, 0.08f, 1.1f, VillagePalette.WoodDark);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 1.15f, 0), new Vector3(0.55f, 0.08f, 0.08f), VillagePalette.WoodWarm);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 1.28f, 0), new Vector3(0.28f, 0.22f, 0.28f), new Color("fff1c2"));
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
