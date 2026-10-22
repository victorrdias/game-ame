using Godot;

namespace Vilanova;

public static class BuildingGhostVisual
{
	private static readonly Color ValidTint = new(0.35f, 0.95f, 0.45f, 0.55f);
	private static readonly Color InvalidTint = new(0.95f, 0.3f, 0.3f, 0.55f);

	public static void ApplyGhostLook(Node3D root, bool isValid)
	{
		var tint = isValid ? ValidTint : InvalidTint;
		ApplyToMeshes(root, tint);
	}

	private static void ApplyToMeshes(Node node, Color tint)
	{
		if (node is MeshInstance3D meshInstance)
		{
			meshInstance.MaterialOverride = CreateGhostMaterial(meshInstance.GetActiveMaterial(0), tint);
			meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		}

		foreach (var child in node.GetChildren())
		{
			ApplyToMeshes(child, tint);
		}
	}

	private static StandardMaterial3D CreateGhostMaterial(Material source, Color tint)
	{
		StandardMaterial3D material;
		if (source is StandardMaterial3D standard)
		{
			material = (StandardMaterial3D)standard.Duplicate();
		}
		else
		{
			material = new StandardMaterial3D();
		}

		material.AlbedoColor = tint;
		material.Metallic = 0.0f;
		material.Roughness = 1.0f;
		material.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
		material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		return material;
	}
}
