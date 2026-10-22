using Godot;

namespace Vilanova;

/// <summary>
/// Shared cozy village colors and surface defaults for procedural placeholders.
/// </summary>
public static class VillagePalette
{
	public static readonly Color WallCream = new("f3e8d4");
	public static readonly Color WallShadow = new("e5d4bc");
	public static readonly Color WoodWarm = new("9c6b43");
	public static readonly Color WoodDark = new("6f4a2e");
	public static readonly Color RoofTerracotta = new("c06840");
	public static readonly Color RoofShadow = new("9a4f32");
	public static readonly Color DoorWood = new("7a4f2f");
	public static readonly Color TrimPaint = new("dcc9a8");
	public static readonly Color WindowGlass = new("8eb5c9");
	public static readonly Color WindowFrame = new("f0e2cc");
	public static readonly Color StoneFoundation = new("b8a898");
	public static readonly Color ChimneyBrick = new("a8654f");
	public static readonly Color FlowerGreen = new("6f9b5a");
	public static readonly Color FlowerAccent = new("e8927c");

	public static readonly Color TrunkBrown = new("7a5230");
	public static readonly Color TrunkDark = new("5c3d24");
	public static readonly Color FoliageGreen = new("4f9160");
	public static readonly Color FoliageLight = new("6db57a");
	public static readonly Color FoliageShadow = new("3a7350");

	public static StandardMaterial3D CreateMatte(Color albedo, float roughness = 0.92f)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = albedo,
			Roughness = roughness,
			Metallic = 0.0f,
			SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
		};
	}
}
