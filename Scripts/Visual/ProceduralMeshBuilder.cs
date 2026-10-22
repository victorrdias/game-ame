using Godot;

namespace Vilanova;

public static class ProceduralMeshBuilder
{
	public static MeshInstance3D AddBox(
		Node3D parent,
		Vector3 position,
		Vector3 size,
		Color color,
		Vector3? rotationDegrees = null,
		float roughness = 0.92f)
	{
		var mesh = new BoxMesh { Size = size };
		mesh.Material = VillagePalette.CreateMatte(color, roughness);

		var instance = new MeshInstance3D
		{
			Mesh = mesh,
			Position = position,
		};

		if (rotationDegrees.HasValue)
		{
			instance.RotationDegrees = rotationDegrees.Value;
		}

		parent.AddChild(instance);
		return instance;
	}

	public static MeshInstance3D AddCylinder(
		Node3D parent,
		Vector3 position,
		float topRadius,
		float bottomRadius,
		float height,
		Color color,
		Vector3? rotationDegrees = null,
		float roughness = 0.92f)
	{
		var mesh = new CylinderMesh
		{
			TopRadius = topRadius,
			BottomRadius = bottomRadius,
			Height = height,
		};
		mesh.Material = VillagePalette.CreateMatte(color, roughness);

		var instance = new MeshInstance3D
		{
			Mesh = mesh,
			Position = position,
		};

		if (rotationDegrees.HasValue)
		{
			instance.RotationDegrees = rotationDegrees.Value;
		}

		parent.AddChild(instance);
		return instance;
	}

	public static MeshInstance3D AddSphere(
		Node3D parent,
		Vector3 position,
		float radius,
		Color color,
		float roughness = 0.92f)
	{
		var mesh = new SphereMesh
		{
			Radius = radius,
			Height = radius * 2.0f,
		};
		mesh.Material = VillagePalette.CreateMatte(color, roughness);

		var instance = new MeshInstance3D
		{
			Mesh = mesh,
			Position = position,
		};
		parent.AddChild(instance);
		return instance;
	}
}
