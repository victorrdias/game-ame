using Godot;

namespace Vilanova;

/// <summary>
/// Gives each lake surface the shared ripple. The bed and the stone banks stay still.
/// </summary>
public partial class RiverWaterMotion : Node
{
	private static ShaderMaterial _shared;

	public override void _Ready()
	{
		_shared ??= new ShaderMaterial
		{
			Shader = GD.Load<Shader>("res://Assets/Shaders/lake_water.gdshader"),
		};
		Apply(GetParent() ?? this);
	}

	private static void Apply(Node node)
	{
		if (node is MeshInstance3D mesh && node.Name.ToString().StartsWith("WaterSurface"))
		{
			mesh.MaterialOverride = _shared;
			mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		}

		foreach (var child in node.GetChildren())
		{
			Apply(child);
		}
	}
}
