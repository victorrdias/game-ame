using Godot;

namespace Vilanova;

public partial class VillageFountain : Node3D
{
	public override void _Ready()
	{
		var root = EnsureVisualRoot();
		ProceduralMeshBuilder.AddCylinder(root, new Vector3(0, 0.12f, 0), 0.95f, 1.05f, 0.18f, VillagePalette.StoneFoundation);
		ProceduralMeshBuilder.AddCylinder(root, new Vector3(0, 0.35f, 0), 0.72f, 0.78f, 0.35f, VillagePalette.WindowGlass, null, 0.25f);
		ProceduralMeshBuilder.AddCylinder(root, new Vector3(0, 0.62f, 0), 0.18f, 0.22f, 0.45f, VillagePalette.StoneFoundation);
		ProceduralMeshBuilder.AddSphere(root, new Vector3(0, 0.92f, 0), 0.16f, VillagePalette.WindowGlass, 0.25f);
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
