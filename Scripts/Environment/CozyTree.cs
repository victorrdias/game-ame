using Godot;

namespace Vilanova;

/// <summary>
/// Lightweight stylized tree. Meshes under VisualRoot for future asset replacement.
/// </summary>
public partial class CozyTree : Node3D
{
	public const string VisualRootName = "VisualRoot";

	[Export] public Color TrunkColor { get; set; } = VillagePalette.TrunkBrown;
	[Export] public Color FoliageColor { get; set; } = VillagePalette.FoliageGreen;
	[Export] public float TrunkHeight { get; set; } = 1.75f;
	[Export] public float FoliageRadius { get; set; } = 1.25f;

	public override void _Ready()
	{
		var visualRoot = GetNodeOrNull<Node3D>(VisualRootName);
		if (visualRoot == null)
		{
			visualRoot = new Node3D { Name = VisualRootName };
			AddChild(visualRoot);
		}

		BuildStylizedTree(visualRoot);
	}

	private void BuildStylizedTree(Node3D root)
	{
		var trunkMesh = new CylinderMesh
		{
			TopRadius = 0.14f,
			BottomRadius = 0.22f,
			Height = TrunkHeight,
			RadialSegments = 8,
		};
		trunkMesh.Material = VillagePalette.CreateMatte(TrunkColor);

		var trunk = new MeshInstance3D
		{
			Mesh = trunkMesh,
			Position = new Vector3(0, TrunkHeight * 0.5f, 0),
		};
		root.AddChild(trunk);

		ProceduralMeshBuilder.AddCylinder(
			root,
			new Vector3(0, 0.12f, 0),
			0.26f,
			0.3f,
			0.18f,
			VillagePalette.TrunkDark);

		var canopyBase = TrunkHeight + FoliageRadius * 0.35f;
		ProceduralMeshBuilder.AddSphere(root, new Vector3(0, canopyBase, 0), FoliageRadius, FoliageColor);
		ProceduralMeshBuilder.AddSphere(
			root,
			new Vector3(0.35f, canopyBase + FoliageRadius * 0.35f, 0.15f),
			FoliageRadius * 0.78f,
			VillagePalette.FoliageLight);
		ProceduralMeshBuilder.AddSphere(
			root,
			new Vector3(-0.3f, canopyBase + FoliageRadius * 0.25f, -0.2f),
			FoliageRadius * 0.72f,
			VillagePalette.FoliageShadow);
		ProceduralMeshBuilder.AddSphere(
			root,
			new Vector3(0.1f, canopyBase + FoliageRadius * 0.65f, -0.05f),
			FoliageRadius * 0.55f,
			FoliageColor);
	}
}
