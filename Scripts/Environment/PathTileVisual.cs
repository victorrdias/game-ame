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

		var tile = (2.0f / TownGridLayout.Subdivisions) * 0.94f;
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.025f, 0), new Vector3(tile, 0.05f, tile), color, roughness: 0.98f);

		if (Style == PathTileStyle.Brick)
		{
			ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.055f, tile * 0.18f), new Vector3(tile * 0.88f, 0.015f, 0.04f), VillagePalette.RoofShadow);
			ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.055f, -tile * 0.18f), new Vector3(tile * 0.88f, 0.015f, 0.04f), VillagePalette.RoofShadow);
		}
		else if (Style == PathTileStyle.Stone)
		{
			ProceduralMeshBuilder.AddBox(root, new Vector3(tile * 0.18f, 0.055f, 0), new Vector3(0.05f, 0.015f, tile * 0.78f), new Color("8f887f"));
			ProceduralMeshBuilder.AddBox(root, new Vector3(-tile * 0.18f, 0.055f, tile * 0.1f), new Vector3(0.05f, 0.015f, tile * 0.55f), new Color("8f887f"));
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
