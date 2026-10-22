using Godot;

namespace Vilanova;

/// <summary>
/// House visual lives in CozyHouse.tscn under VisualRoot/BlenderModel (GLB).
/// Procedural meshes are only built when AllowProceduralFallback is on and the GLB child is missing.
/// </summary>
public partial class CozyHouse : Node3D
{
	public const string VisualRootName = "VisualRoot";
	public const string BlenderModelName = "BlenderModel";

	[Export] public Color WallColor { get; set; } = VillagePalette.WallCream;
	[Export] public Color RoofColor { get; set; } = VillagePalette.RoofTerracotta;
	[Export] public Color DoorColor { get; set; } = VillagePalette.DoorWood;
	[Export] public Color TrimColor { get; set; } = VillagePalette.TrimPaint;

	[Export] public bool AllowProceduralFallback { get; set; }

	public override void _Ready()
	{
		var visualRoot = EnsureVisualRoot();
		if (visualRoot.GetNodeOrNull(BlenderModelName) != null || visualRoot.GetChildCount() > 0)
		{
			return;
		}

		if (AllowProceduralFallback)
		{
			BuildVillageHouse(visualRoot);
		}
	}

	private Node3D EnsureVisualRoot()
	{
		var visualRoot = GetNodeOrNull<Node3D>(VisualRootName);
		if (visualRoot != null)
		{
			return visualRoot;
		}

		visualRoot = new Node3D { Name = VisualRootName };
		AddChild(visualRoot);
		return visualRoot;
	}

	private void BuildVillageHouse(Node3D root)
	{
		BuildFoundation(root);
		BuildWallsAndTimbers(root);
		BuildDoor(root);
		BuildWindows(root);
		BuildRoof(root);
		BuildChimney(root);
		BuildDecor(root);
	}

	private void BuildFoundation(Node3D root)
	{
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.08f, 0), new Vector3(3.5f, 0.16f, 3.3f), VillagePalette.StoneFoundation);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.18f, 0), new Vector3(3.25f, 0.06f, 3.05f), WallColor);
	}

	private void BuildWallsAndTimbers(Node3D root)
	{
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 1.2f, 0), new Vector3(3.15f, 2.05f, 2.95f), WallColor);

		var timberDepth = 0.12f;
		var timberWidth = 0.18f;
		var wallHalf = 1.47f;
		var wallHeight = 2.05f;
		var yMid = 1.2f;

		ProceduralMeshBuilder.AddBox(root, new Vector3(-wallHalf, yMid, 0), new Vector3(timberWidth, wallHeight, 3.0f), TrimColor);
		ProceduralMeshBuilder.AddBox(root, new Vector3(wallHalf, yMid, 0), new Vector3(timberWidth, wallHeight, 3.0f), TrimColor);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, yMid, -wallHalf), new Vector3(3.0f, wallHeight, timberWidth), TrimColor);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, yMid, wallHalf), new Vector3(3.0f, wallHeight, timberWidth), TrimColor);

		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.75f, wallHalf + timberDepth * 0.5f), new Vector3(3.0f, 0.12f, timberDepth), WoodWarm);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 1.65f, wallHalf + timberDepth * 0.5f), new Vector3(3.0f, 0.12f, timberDepth), WoodWarm);
	}

	private void BuildDoor(Node3D root)
	{
		var frontZ = 1.52f;
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.95f, frontZ), new Vector3(1.05f, 1.75f, 0.1f), WindowFrame);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.92f, frontZ + 0.03f), new Vector3(0.78f, 1.55f, 0.08f), DoorColor);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0.22f, 0.95f, frontZ + 0.08f), new Vector3(0.07f, 0.07f, 0.04f), WoodDark);
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 0.28f, frontZ + 0.04f), new Vector3(0.95f, 0.1f, 0.22f), VillagePalette.StoneFoundation);
	}

	private void BuildWindows(Node3D root)
	{
		AddWindow(root, new Vector3(-0.95f, 1.45f, 1.52f));
		AddWindow(root, new Vector3(0.95f, 1.45f, 1.52f));
		AddWindow(root, new Vector3(1.52f, 1.45f, -0.35f), rotationY: -90.0f);
	}

	private void AddWindow(Node3D root, Vector3 position, float rotationY = 0.0f)
	{
		ProceduralMeshBuilder.AddBox(
			root,
			position,
			new Vector3(0.72f, 0.72f, 0.1f),
			WindowFrame,
			new Vector3(0, rotationY, 0));

		var glassPos = position + new Vector3(0, 0, 0.03f).Rotated(Vector3.Up, Mathf.DegToRad(rotationY));
		ProceduralMeshBuilder.AddBox(
			root,
			glassPos,
			new Vector3(0.52f, 0.52f, 0.06f),
			VillagePalette.WindowGlass,
			new Vector3(0, rotationY, 0),
			roughness: 0.35f);

		var sillPos = position + new Vector3(0, -0.42f, 0.02f).Rotated(Vector3.Up, Mathf.DegToRad(rotationY));
		ProceduralMeshBuilder.AddBox(
			root,
			sillPos,
			new Vector3(0.78f, 0.08f, 0.14f),
			WoodWarm,
			new Vector3(0, rotationY, 0));
	}

	private void BuildRoof(Node3D root)
	{
		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 2.22f, 0), new Vector3(3.55f, 0.12f, 3.35f), RoofColor);

		var ridgeY = 2.55f;
		ProceduralMeshBuilder.AddBox(
			root,
			new Vector3(0, ridgeY, 0.95f),
			new Vector3(3.65f, 0.14f, 2.35f),
			RoofColor,
			new Vector3(-36, 0, 0));

		ProceduralMeshBuilder.AddBox(
			root,
			new Vector3(0, ridgeY, -0.95f),
			new Vector3(3.65f, 0.14f, 2.35f),
			VillagePalette.RoofShadow,
			new Vector3(36, 0, 0));

		ProceduralMeshBuilder.AddBox(root, new Vector3(0, 2.78f, 0), new Vector3(3.75f, 0.1f, 0.22f), RoofColor);
	}

	private void BuildChimney(Node3D root)
	{
		ProceduralMeshBuilder.AddBox(root, new Vector3(-1.05f, 2.55f, -0.55f), new Vector3(0.38f, 1.05f, 0.38f), VillagePalette.ChimneyBrick);
		ProceduralMeshBuilder.AddBox(root, new Vector3(-1.05f, 3.12f, -0.55f), new Vector3(0.46f, 0.1f, 0.46f), VillagePalette.StoneFoundation);
	}

	private void BuildDecor(Node3D root)
	{
		var frontZ = 1.58f;
		ProceduralMeshBuilder.AddBox(root, new Vector3(-0.95f, 0.95f, frontZ), new Vector3(0.62f, 0.14f, 0.18f), WoodWarm);
		ProceduralMeshBuilder.AddBox(root, new Vector3(-0.95f, 1.02f, frontZ + 0.02f), new Vector3(0.52f, 0.08f, 0.12f), VillagePalette.FlowerGreen);
		ProceduralMeshBuilder.AddBox(root, new Vector3(-0.82f, 1.06f, frontZ + 0.05f), new Vector3(0.08f, 0.08f, 0.06f), VillagePalette.FlowerAccent);
		ProceduralMeshBuilder.AddBox(root, new Vector3(-1.08f, 1.05f, frontZ + 0.05f), new Vector3(0.08f, 0.08f, 0.06f), VillagePalette.FlowerAccent);

		ProceduralMeshBuilder.AddBox(root, new Vector3(1.35f, 0.05f, 1.35f), new Vector3(0.35f, 0.05f, 0.35f), VillagePalette.StoneFoundation);
		ProceduralMeshBuilder.AddBox(root, new Vector3(1.55f, 0.05f, 1.15f), new Vector3(0.28f, 0.05f, 0.28f), VillagePalette.StoneFoundation);
	}

	private static Color WoodWarm => VillagePalette.WoodWarm;
	private static Color WoodDark => VillagePalette.WoodDark;
	private static Color WindowFrame => VillagePalette.WindowFrame;
}
