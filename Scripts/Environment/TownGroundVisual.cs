using Godot;

namespace Vilanova;

/// <summary>
/// Pixel floor imported from the town-ground GLB. Visual only: collision stays on the valley.
/// </summary>
public partial class TownGroundVisual : Node3D
{
	public override void _Ready()
	{
		foreach (var node in FindChildren("*", "MeshInstance3D", true, false))
		{
			if (node is not MeshInstance3D mesh)
			{
				continue;
			}

			var tuft = mesh.Name.ToString().StartsWith("Tuft");
			mesh.CastShadow = tuft
				? GeometryInstance3D.ShadowCastingSetting.On
				: GeometryInstance3D.ShadowCastingSetting.Off;

			if (mesh.Mesh == null)
			{
				continue;
			}

			for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
			{
				PrepareSurface(mesh, surface, tuft);
			}
		}
	}

	private static void PrepareSurface(MeshInstance3D mesh, int surface, bool tuft)
	{
		var source = mesh.GetActiveMaterial(surface) as StandardMaterial3D;
		if (source == null)
		{
			return;
		}

		var material = (StandardMaterial3D)source.Duplicate();
		material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
		material.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
		material.Roughness = 1.0f;
		material.Metallic = 0.0f;
		if (tuft)
		{
			material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
			material.AlphaScissorThreshold = 0.5f;
			material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		}

		mesh.SetSurfaceOverrideMaterial(surface, material);
	}
}
