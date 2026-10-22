using Godot;

namespace Vilanova;

public enum PathTileStyle
{
	Dirt,
	Stone,
	Brick,
}

public partial class PathTileVisual : Node3D
{
	[Export] public PathTileStyle Style { get; set; } = PathTileStyle.Dirt;

	public override void _Ready()
	{
		var root = EnsureVisualRoot();
		var color = Style switch
		{
			PathTileStyle.Stone => new Color("a8a29a"),
			PathTileStyle.Brick => new Color("b86a4a"),
			_ => new Color("9a744c"),
		};

		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.025f, 0), new Vector3(1.92f, 0.05f, 1.92f), color, roughness: 0.98f);

		if (Style == PathTileStyle.Brick)
		{
			ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.055f, 0.35f), new Vector3(1.7f, 0.015f, 0.08f), VillagePalette.RoofShadow);
			ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.055f, -0.35f), new Vector3(1.7f, 0.015f, 0.08f), VillagePalette.RoofShadow);
		}
		else if (Style == PathTileStyle.Stone)
		{
			ProceduralMeshBuilder.AddBox(root, new Vector3(0.35f, 0.055f, 0), new Vector3(0.12f, 0.015f, 1.5f), new Color("8f887f"));
			ProceduralMeshBuilder.AddBox(root, new Vector3(-0.35f, 0.055f, 0.2f), new Vector3(0.12f, 0.015f, 1.1f), new Color("8f887f"));
		}
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
