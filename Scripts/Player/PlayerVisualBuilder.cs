using Godot;

namespace Vilanova;

public static class PlayerVisualBuilder
{
	public static void Build(Node3D visualRoot, Color? torsoColor = null, Color? pantsColor = null, Color? skinColor = null)
	{
		var torso = torsoColor ?? VillagePalette.WallCream;
		var chest = torsoColor ?? VillagePalette.TrimPaint;
		var pants = pantsColor ?? VillagePalette.DoorWood;
		var skin = skinColor ?? new Color("f5d5b8");

		ProceduralMeshBuilder.AddBox(visualRoot, new Vector3(0, 0.52f, 0), new Vector3(0.42f, 0.55f, 0.28f), torso);
		ProceduralMeshBuilder.AddBox(visualRoot, new Vector3(0, 0.95f, 0), new Vector3(0.36f, 0.38f, 0.3f), chest);
		ProceduralMeshBuilder.AddSphere(visualRoot, new Vector3(0, 1.28f, 0), 0.22f, skin);
		ProceduralMeshBuilder.AddBox(visualRoot, new Vector3(-0.1f, 1.3f, 0.16f), new Vector3(0.06f, 0.04f, 0.02f), VillagePalette.WoodDark);
		ProceduralMeshBuilder.AddBox(visualRoot, new Vector3(0.1f, 1.3f, 0.16f), new Vector3(0.06f, 0.04f, 0.02f), VillagePalette.WoodDark);
		ProceduralMeshBuilder.AddBox(visualRoot, new Vector3(-0.12f, 0.18f, 0), new Vector3(0.14f, 0.36f, 0.16f), pants);
		ProceduralMeshBuilder.AddBox(visualRoot, new Vector3(0.12f, 0.18f, 0), new Vector3(0.14f, 0.36f, 0.16f), pants);
	}
}
