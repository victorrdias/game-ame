using Godot;

namespace Vilanova;

/// <summary>
/// Draws a simple line grid on the ground plane (no custom shaders).
/// </summary>
public partial class TerrainGrid : Node3D
{
	[Export] public float TerrainSize { get; set; } = TerrainHeightField.ValleySize;
	[Export] public float CellSize { get; set; } = 2.0f;
	[Export] public float LineHeight { get; set; } = 0.03f;
	[Export] public Color LineColor { get; set; } = new(0.55f, 0.42f, 0.22f, 0.22f);

	public override void _Ready()
	{
		var gridMesh = new MeshInstance3D
		{
			Mesh = BuildLineMesh(),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		AddChild(gridMesh);
		Visible = false;
	}

	public void SetVisibleForConstruction(bool visible)
	{
		Visible = visible;
	}

	private ArrayMesh BuildLineMesh()
	{
		var field = TerrainHeightField.CreateValley(TerrainSize, CellSize);
		var surface = new SurfaceTool();
		surface.Begin(Mesh.PrimitiveType.Lines);

		for (var x = 0; x < field.CellCount; x++)
		{
			for (var z = 0; z < field.CellCount; z++)
			{
				var cell = new Vector2I(x, z);
				if (!field.IsStandable(cell))
				{
					continue;
				}

				var x0 = field.CellMin(x);
				var z0 = field.CellMin(z);
				var x1 = x0 + CellSize;
				var z1 = z0 + CellSize;
				var y00 = field.CornerY(x, z, 0, 0) + LineHeight;
				var y10 = field.CornerY(x, z, 1, 0) + LineHeight;
				var y01 = field.CornerY(x, z, 0, 1) + LineHeight;
				var y11 = field.CornerY(x, z, 1, 1) + LineHeight;
				surface.AddVertex(new Vector3(x0, y00, z0));
				surface.AddVertex(new Vector3(x1, y10, z0));
				surface.AddVertex(new Vector3(x0, y00, z0));
				surface.AddVertex(new Vector3(x0, y01, z1));
				if (x == field.CellCount - 1)
				{
					surface.AddVertex(new Vector3(x1, y10, z0));
					surface.AddVertex(new Vector3(x1, y11, z1));
				}

				if (z == field.CellCount - 1)
				{
					surface.AddVertex(new Vector3(x0, y01, z1));
					surface.AddVertex(new Vector3(x1, y11, z1));
				}
			}
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
