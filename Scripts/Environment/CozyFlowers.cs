using Godot;

namespace Vilanova;

public partial class CozyFlowers : Node3D
{
	public override void _Ready()
	{
		var root = EnsureVisualRoot();
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.04f, 0), new Vector3(0.9f, 0.06f, 0.9f), VillagePalette.FlowerGreen);
		for (var i = 0; i < 5; i++)
		{
			var angle = i * Mathf.Tau / 5.0f;
			var pos = new Vector3(Mathf.Cos(angle) * 0.25f, 0.18f, Mathf.Sin(angle) * 0.25f);
			ProceduralMeshBuilder.AddSphere(root, pos, 0.1f, i % 2 == 0 ? VillagePalette.FlowerAccent : new Color("f2d16b"));
		}

		ProceduralMeshBuilder.AddCylinder(root, new Vector3(0, 0.12f, 0), 0.03f, 0.03f, 0.12f, VillagePalette.FoliageGreen);
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
