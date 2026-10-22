using Godot;

namespace Vilanova;

public static class HorizontalReach
{
	public static float DistanceXZ(Vector3 fromWorld, Vector3 toWorld)
	{
		var dx = fromWorld.X - toWorld.X;
		var dz = fromWorld.Z - toWorld.Z;
		return Mathf.Sqrt(dx * dx + dz * dz);
	}
}
