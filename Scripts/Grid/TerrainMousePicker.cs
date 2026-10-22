using Godot;

namespace Vilanova;

public static class TerrainMousePicker
{
	private const float RayLength = 500.0f;
	private const uint TerrainCollisionLayer = 1;

	public static bool TryPickGroundPoint(Viewport viewport, Camera3D camera, out Vector3 hitPoint)
	{
		hitPoint = Vector3.Zero;
		if (viewport == null || camera == null)
		{
			return false;
		}

		var mousePosition = GetViewportMousePosition(viewport);
		var rayOrigin = camera.ProjectRayOrigin(mousePosition);
		var rayDirection = camera.ProjectRayNormal(mousePosition);

		if (TryPhysicsRay(viewport, rayOrigin, rayDirection, out hitPoint))
		{
			return true;
		}

		return TryGroundPlane(rayOrigin, rayDirection, out hitPoint);
	}

	private static Vector2 GetViewportMousePosition(Viewport viewport)
	{
		// get_mouse_position is already in content coordinates. ProjectRay* applies
		// the stretch transform itself (canvas_items, 1280p UI on a 2K window).
		return viewport.GetMousePosition();
	}

	private static bool TryPhysicsRay(Viewport viewport, Vector3 origin, Vector3 direction, out Vector3 hitPoint)
	{
		hitPoint = Vector3.Zero;
		var spaceState = viewport.GetWorld3D()?.DirectSpaceState;
		if (spaceState == null)
		{
			return false;
		}

		var to = origin + direction * RayLength;
		var query = PhysicsRayQueryParameters3D.Create(origin, to);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		query.CollisionMask = TerrainCollisionLayer;

		var hit = spaceState.IntersectRay(query);
		if (hit.Count == 0)
		{
			return false;
		}

		hitPoint = (Vector3)hit["position"];
		return true;
	}

	private static bool TryGroundPlane(Vector3 origin, Vector3 direction, out Vector3 hitPoint)
	{
		hitPoint = Vector3.Zero;
		if (Mathf.Abs(direction.Y) < 0.0001f)
		{
			return false;
		}

		var t = -origin.Y / direction.Y;
		if (t < 0.0f)
		{
			return false;
		}

		hitPoint = origin + direction * t;
		return true;
	}
}
