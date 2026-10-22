using Godot;

namespace Vilanova;

public partial class CozyBush : Node3D
{
	public override void _Ready()
	{
		var root = EnsureVisualRoot();
		ProceduralMeshBuilder.AddSphere(root, new Vector3(0, 0.35f, 0), 0.55f, VillagePalette.FoliageShadow);
		ProceduralMeshBuilder.AddSphere(root, new Vector3(0.25f, 0.45f, 0.1f), 0.42f, VillagePalette.FoliageGreen);
		ProceduralMeshBuilder.AddSphere(root, new Vector3(-0.22f, 0.42f, -0.12f), 0.38f, VillagePalette.FoliageLight);
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
