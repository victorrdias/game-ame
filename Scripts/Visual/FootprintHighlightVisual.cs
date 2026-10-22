using System.Collections.Generic;
using Godot;

namespace Vilanova;

/// <summary>
/// Flat footprint overlay on the ground for placement, demolition, and interaction hints.
/// </summary>
public partial class FootprintHighlightVisual : Node3D
{
	[Export] public float CellSize { get; set; } = 2.0f;
	[Export] public float TerrainSize { get; set; } = TerrainHeightField.ValleySize;
	[Export] public float SurfaceHeight { get; set; } = 0.045f;

	public System.Func<Vector2I, float> HeightAt { get; set; }

	private GridSystem _grid = null!;
	private MeshInstance3D _meshInstance = null!;
	private StandardMaterial3D _material = null!;
	private string _lastFootprintKey = string.Empty;
	private Color _lastColor;

	public override void _Ready()
	{
		_grid = new GridSystem(TerrainSize, CellSize);
		_meshInstance = new MeshInstance3D
		{
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		AddChild(_meshInstance);
		_material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		};
		Visible = false;
	}

	public void HideHighlight()
	{
		Visible = false;
		_lastFootprintKey = string.Empty;
	}

	public void ShowFootprint(IEnumerable<Vector2I> cells, Color color)
	{
		var key = BuildFootprintKey(cells);
		if (key == _lastFootprintKey && _lastColor == color && Visible)
		{
			return;
		}

		_lastFootprintKey = key;
		_lastColor = color;

		var surface = new SurfaceTool();
		surface.Begin(Mesh.PrimitiveType.Triangles);

		var inset = CellSize * 0.06f;
		var half = CellSize * 0.5f - inset;

		foreach (var cell in cells)
		{
			var center = _grid.CellToWorldCenter(cell);
			var y = (HeightAt?.Invoke(cell) ?? 0.0f) + SurfaceHeight;
			var x0 = center.X - half;
			var x1 = center.X + half;
			var z0 = center.Z - half;
			var z1 = center.Z + half;

			AddQuad(surface, new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1));
		}

		_material.AlbedoColor = color;
		surface.SetMaterial(_material);
		_meshInstance.Mesh = surface.Commit();
		Visible = true;
	}

	public void ShowRotatedFootprint(Vector2I originCell, Vector2I baseSize, int rotationSteps, Color color)
	{
		ShowFootprint(FootprintHelper.EnumerateFootprint(originCell, baseSize, rotationSteps), color);
	}

	private static void AddQuad(
		SurfaceTool surface,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector3 d)
	{
		surface.AddVertex(a);
		surface.AddVertex(b);
		surface.AddVertex(c);
		surface.AddVertex(a);
		surface.AddVertex(c);
		surface.AddVertex(d);
	}

	private static string BuildFootprintKey(IEnumerable<Vector2I> cells)
	{
		var parts = new System.Collections.Generic.List<string>();
		foreach (var cell in cells)
		{
			parts.Add($"{cell.X},{cell.Y}");
		}

		parts.Sort();
		return string.Join("|", parts);
	}
}
