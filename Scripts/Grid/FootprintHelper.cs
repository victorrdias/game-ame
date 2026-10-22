using System.Collections.Generic;
using Godot;

namespace Vilanova;

public static class FootprintHelper
{
	public static int NormalizeRotationSteps(int steps)
	{
		return ((steps % 4) + 4) % 4;
	}

	public static Vector2I GetRotatedSize(Vector2I baseSize, int rotationSteps)
	{
		var steps = NormalizeRotationSteps(rotationSteps);
		return steps % 2 == 1 ? new Vector2I(baseSize.Y, baseSize.X) : baseSize;
	}

	public static Vector2I OriginFromCenterCell(Vector2I centerCell, Vector2I rotatedSize)
	{
		return new Vector2I(centerCell.X - rotatedSize.X / 2, centerCell.Y - rotatedSize.Y / 2);
	}

	public static Vector2I RotateLocalOffset(Vector2I local, Vector2I baseSize, int rotationSteps)
	{
		var steps = NormalizeRotationSteps(rotationSteps);
		var lx = local.X;
		var lz = local.Y;
		return steps switch
		{
			0 => new Vector2I(lx, lz),
			1 => new Vector2I(baseSize.Y - 1 - lz, lx),
			2 => new Vector2I(baseSize.X - 1 - lx, baseSize.Y - 1 - lz),
			3 => new Vector2I(lz, baseSize.X - 1 - lx),
			_ => new Vector2I(lx, lz),
		};
	}

	public static IEnumerable<Vector2I> EnumerateFootprint(
		Vector2I originCell,
		Vector2I baseSize,
		int rotationSteps)
	{
		for (var lx = 0; lx < baseSize.X; lx++)
		{
			for (var lz = 0; lz < baseSize.Y; lz++)
			{
				var offset = RotateLocalOffset(new Vector2I(lx, lz), baseSize, rotationSteps);
				yield return originCell + offset;
			}
		}
	}

	public static float RotationDegrees(int rotationSteps)
	{
		return NormalizeRotationSteps(rotationSteps) * 90.0f;
	}
}
