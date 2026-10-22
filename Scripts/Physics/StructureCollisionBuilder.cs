using Godot;

namespace Vilanova;

public static class StructureCollisionBuilder
{
	public static void EnsureStructureCollision(
		Node3D visualRoot,
		BuildingDefinition definition,
		int rotationSteps,
		float cellSize)
	{
		if (!definition.BlocksMovement)
		{
			return;
		}

		if (visualRoot.GetNodeOrNull<StaticBody3D>("StructureBody") != null)
		{
			return;
		}

		var halfExtents = GetCollisionHalfExtents(definition, rotationSteps, cellSize);
		var width = halfExtents.X * 2.0f;
		var depth = halfExtents.Y * 2.0f;
		var height = GetCollisionHeight(definition, rotationSteps, cellSize);

		var body = new StaticBody3D
		{
			Name = "StructureBody",
			CollisionLayer = CollisionLayers.Structure,
			CollisionMask = 0,
		};
		var shape = new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = new Vector3(width, height, depth) },
			Position = new Vector3(0, height * 0.5f, 0),
		};
		body.AddChild(shape);
		visualRoot.AddChild(body);
	}

	public static void DisableCollisionRecursively(Node node)
	{
		if (node is CollisionObject3D collisionObject)
		{
			collisionObject.CollisionLayer = 0;
			collisionObject.CollisionMask = 0;
		}

		foreach (var child in node.GetChildren())
		{
			DisableCollisionRecursively(child);
		}
	}

	public static Vector2 GetCollisionHalfExtents(BuildingDefinition definition, int rotationSteps, float cellSize)
	{
		// The box lives on the yawed node, so its local size stays the unrotated footprint.
		// The node yaw turns it with the model. Swapping here as well puts the hitbox sideways.
		_ = rotationSteps;
		var size = definition.SizeInCells;
		return new Vector2(
			size.X * cellSize * 0.92f * 0.5f,
			size.Y * cellSize * 0.92f * 0.5f);
	}

	private static float GetCollisionHeight(BuildingDefinition definition, int rotationSteps, float cellSize)
	{
		if (definition.CollisionHeight > 0.0f)
		{
			return definition.CollisionHeight;
		}

		if (definition.Layer == OccupancyLayer.Path)
		{
			return 0.2f;
		}

		var rotated = FootprintHelper.GetRotatedSize(definition.SizeInCells, rotationSteps);
		var span = Mathf.Max(rotated.X, rotated.Y) * cellSize;
		if (span >= 3.2f)
		{
			return 3.2f;
		}

		if (span >= 1.2f)
		{
			return 2.0f;
		}

		return 1.15f;
	}
}
