using Godot;

namespace Vilanova;

/// <summary>
/// Draws a simple line grid on the ground plane (no custom shaders).
/// </summary>
public partial class TerrainGrid : Node3D
{
	[Export] public float TerrainSize { get; set; } = 40.0f;
	[Export] public float CellSize { get; set; } = 2.0f;
	[Export] public float LineHeight { get; set; } = 0.03f;
	[Export] public Color LineColor { get; set; } = new(0.42f, 0.54f, 0.36f, 0.38f);

	public override void _Ready()
	{
		var gridMesh = new MeshInstance3D
		{
			Mesh = BuildLineMesh(),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		AddChild(gridMesh);
	}

	private ArrayMesh BuildLineMesh()
	{
		var half = TerrainSize * 0.5f;
		var lineCount = Mathf.CeilToInt(TerrainSize / CellSize) + 1;

		var surface = new SurfaceTool();
		surface.Begin(Mesh.PrimitiveType.Lines);

		for (var i = 0; i < lineCount; i++)
		{
			var offset = -half + i * CellSize;
			surface.AddVertex(new Vector3(-half, LineHeight, offset));
			surface.AddVertex(new Vector3(half, LineHeight, offset));
			surface.AddVertex(new Vector3(offset, LineHeight, -half));
			surface.AddVertex(new Vector3(offset, LineHeight, half));
		}

		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = LineColor,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		};
		surface.SetMaterial(material);

		return surface.Commit();
	}
}
