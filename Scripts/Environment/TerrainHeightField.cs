using System;
using System.Collections.Generic;
using Godot;

namespace Vilanova;

/// <summary>
/// Discrete heights on the 2m visual grid. Placement stays on XZ; Y is sampled from here.
/// Plateaus are the only buildable surfaces. Ramps are the only walkable level changes.
/// The rim is the playable boundary: steep, visible, and closed to construction and A*.
/// </summary>
public sealed class TerrainHeightField
{
	public float TerrainSize { get; }
	public float CellSize { get; }
	public float Step { get; }
	public int CellCount { get; }

	private readonly TerrainCellKind[] _kinds;
	private readonly int[] _levels;
	private readonly TerrainRampDirection[] _ramps;
	private readonly float _half;

	public TerrainHeightField(float terrainSize, float cellSize, float step = 1.0f)
	{
		TerrainSize = terrainSize;
		CellSize = cellSize;
		Step = step;
		_half = terrainSize * 0.5f;
		CellCount = Mathf.FloorToInt(terrainSize / cellSize);
		var count = CellCount * CellCount;
		_kinds = new TerrainCellKind[count];
		_levels = new int[count];
		_ramps = new TerrainRampDirection[count];
	}

	public static TerrainHeightField Flat(float terrainSize, float cellSize)
	{
		return new TerrainHeightField(terrainSize, cellSize);
	}

	public const float ValleySize = 240.0f;

	public static TerrainHeightField CreateValley(float terrainSize = ValleySize, float cellSize = 2.0f)
	{
		var field = new TerrainHeightField(terrainSize, cellSize);
		var last = field.CellCount - 1;
		for (var x = 0; x < field.CellCount; x++)
		{
			for (var z = 0; z < field.CellCount; z++)
			{
				if (x <= 1 || z <= 1 || x >= last - 1 || z >= last - 1)
				{
					field.Set(x, z, TerrainCellKind.Rim, 4, TerrainRampDirection.None);
				}
			}
		}

		// 240m valley. The old 40m town stays a flat 0m green around the cottages
		// (about cells 46..73, world ±28m). Hills and a hollow sit outside that.
		field.FillRect(14, 36, 44, 78, TerrainCellKind.Plateau, 1, TerrainRampDirection.None);
		field.FillRect(37, 37, 54, 62, TerrainCellKind.Ramp, 0, TerrainRampDirection.NegativeX);

		field.FillRect(82, 104, 48, 78, TerrainCellKind.Plateau, 1, TerrainRampDirection.None);
		field.FillRect(96, 110, 56, 74, TerrainCellKind.Plateau, 2, TerrainRampDirection.None);
		field.FillRect(81, 81, 58, 66, TerrainCellKind.Ramp, 0, TerrainRampDirection.PositiveX);
		field.FillRect(95, 95, 60, 66, TerrainCellKind.Ramp, 1, TerrainRampDirection.PositiveX);

		field.FillRect(40, 80, 82, 94, TerrainCellKind.Plateau, 1, TerrainRampDirection.None);
		field.FillRect(46, 76, 96, 110, TerrainCellKind.Plateau, 2, TerrainRampDirection.None);
		field.FillRect(54, 66, 102, 108, TerrainCellKind.Plateau, 3, TerrainRampDirection.None);
		field.FillRect(54, 64, 81, 81, TerrainCellKind.Ramp, 0, TerrainRampDirection.PositiveZ);
		field.FillRect(56, 64, 95, 95, TerrainCellKind.Ramp, 1, TerrainRampDirection.PositiveZ);
		field.FillRect(56, 62, 101, 101, TerrainCellKind.Ramp, 2, TerrainRampDirection.PositiveZ);

		field.FillRect(18, 38, 84, 100, TerrainCellKind.Plateau, 1, TerrainRampDirection.None);
		field.FillRect(20, 34, 90, 106, TerrainCellKind.Plateau, 2, TerrainRampDirection.None);
		field.FillRect(22, 30, 98, 106, TerrainCellKind.Plateau, 3, TerrainRampDirection.None);
		field.FillRect(22, 30, 83, 83, TerrainCellKind.Ramp, 0, TerrainRampDirection.PositiveZ);
		field.FillRect(22, 30, 89, 89, TerrainCellKind.Ramp, 1, TerrainRampDirection.PositiveZ);
		field.FillRect(24, 28, 97, 97, TerrainCellKind.Ramp, 2, TerrainRampDirection.PositiveZ);

		field.FillRect(86, 112, 18, 42, TerrainCellKind.Plateau, 1, TerrainRampDirection.None);
		field.FillRect(96, 110, 24, 36, TerrainCellKind.Plateau, 2, TerrainRampDirection.None);
		field.FillRect(85, 85, 26, 32, TerrainCellKind.Ramp, 0, TerrainRampDirection.PositiveX);
		field.FillRect(95, 95, 28, 32, TerrainCellKind.Ramp, 1, TerrainRampDirection.PositiveX);

		field.FillRect(54, 72, 16, 34, TerrainCellKind.Plateau, -1, TerrainRampDirection.None);
		field.FillRect(60, 66, 35, 35, TerrainCellKind.Ramp, -1, TerrainRampDirection.PositiveZ);
		field.FillRect(90, 104, 84, 98, TerrainCellKind.Plateau, -1, TerrainRampDirection.None);
		field.FillRect(89, 89, 88, 94, TerrainCellKind.Ramp, -1, TerrainRampDirection.NegativeX);
		return field;
	}

	public bool InBounds(Vector2I cell)
	{
		return cell.X >= 0 && cell.Y >= 0 && cell.X < CellCount && cell.Y < CellCount;
	}

	public TerrainCellKind KindAt(Vector2I cell)
	{
		return _kinds[Index(cell)];
	}

	public int LevelAt(Vector2I cell)
	{
		return _levels[Index(cell)];
	}

	public TerrainRampDirection RampAt(Vector2I cell)
	{
		return _ramps[Index(cell)];
	}

	public bool IsStandable(Vector2I cell)
	{
		if (!InBounds(cell))
		{
			return false;
		}

		var kind = KindAt(cell);
		return kind == TerrainCellKind.Plateau || kind == TerrainCellKind.Ramp;
	}

	public float SurfaceY(Vector2I cell)
	{
		return LevelAt(cell) * Step;
	}

	public float SampleY(float worldX, float worldZ)
	{
		var cell = WorldToCell(worldX, worldZ);
		if (!InBounds(cell))
		{
			return 0.0f;
		}

		if (KindAt(cell) != TerrainCellKind.Ramp)
		{
			return SurfaceY(cell);
		}

		var localX = Mathf.Clamp((worldX - CellMin(cell.X)) / CellSize, 0.0f, 1.0f);
		var localZ = Mathf.Clamp((worldZ - CellMin(cell.Y)) / CellSize, 0.0f, 1.0f);
		var along = RampAt(cell) switch
		{
			TerrainRampDirection.PositiveX => localX,
			TerrainRampDirection.NegativeX => 1.0f - localX,
			TerrainRampDirection.PositiveZ => localZ,
			TerrainRampDirection.NegativeZ => 1.0f - localZ,
			_ => 0.0f,
		};
		return (LevelAt(cell) + along) * Step;
	}

	public Vector3 WithSurface(Vector3 world)
	{
		world.Y = SampleY(world.X, world.Z);
		return world;
	}

	public float CornerY(int cellX, int cellZ, int cornerX, int cornerZ)
	{
		var cell = new Vector2I(cellX, cellZ);
		var baseY = SurfaceY(cell);
		if (KindAt(cell) != TerrainCellKind.Ramp)
		{
			return baseY;
		}

		var high = RampAt(cell) switch
		{
			TerrainRampDirection.PositiveX => cornerX == 1,
			TerrainRampDirection.NegativeX => cornerX == 0,
			TerrainRampDirection.PositiveZ => cornerZ == 1,
			TerrainRampDirection.NegativeZ => cornerZ == 0,
			_ => false,
		};
		return baseY + (high ? Step : 0.0f);
	}

	public bool AllowsStep(Vector2I from, Vector2I to)
	{
		if (!IsStandable(from) || !IsStandable(to))
		{
			return false;
		}

		var fromKind = KindAt(from);
		var toKind = KindAt(to);
		if (fromKind == TerrainCellKind.Plateau && toKind == TerrainCellKind.Plateau)
		{
			return LevelAt(from) == LevelAt(to);
		}

		if (fromKind == TerrainCellKind.Ramp && toKind == TerrainCellKind.Ramp)
		{
			return LevelAt(from) == LevelAt(to) && RampAt(from) == RampAt(to);
		}

		var rampCell = fromKind == TerrainCellKind.Ramp ? from : to;
		var plateauCell = fromKind == TerrainCellKind.Ramp ? to : from;
		var towardHigh = StepOffset(RampAt(rampCell));
		if (towardHigh == Vector2I.Zero || KindAt(plateauCell) != TerrainCellKind.Plateau)
		{
			return false;
		}

		if (plateauCell == rampCell - towardHigh && LevelAt(plateauCell) == LevelAt(rampCell))
		{
			return true;
		}

		return plateauCell == rampCell + towardHigh && LevelAt(plateauCell) == LevelAt(rampCell) + 1;
	}

	public TerrainFootprintStatus EvaluateFootprint(
		IEnumerable<Vector2I> placementCells,
		Func<Vector2I, Vector2I> placementToVisual,
		out float height)
	{
		height = 0.0f;
		var sawCell = false;
		var level = 0;
		foreach (var placementCell in placementCells)
		{
			var visual = placementToVisual(placementCell);
			if (!InBounds(visual))
			{
				return TerrainFootprintStatus.Uneven;
			}

			if (KindAt(visual) == TerrainCellKind.Rim)
			{
				return TerrainFootprintStatus.OutOfPlay;
			}

			if (KindAt(visual) != TerrainCellKind.Plateau)
			{
				return TerrainFootprintStatus.Uneven;
			}

			if (!sawCell)
			{
				level = LevelAt(visual);
				sawCell = true;
			}
			else if (level != LevelAt(visual))
			{
				return TerrainFootprintStatus.Uneven;
			}
		}

		if (!sawCell)
		{
			return TerrainFootprintStatus.Uneven;
		}

		height = level * Step;
		return TerrainFootprintStatus.Ok;
	}

	public Vector2I WorldToCell(float worldX, float worldZ)
	{
		var x = Mathf.FloorToInt((worldX + _half) / CellSize);
		var z = Mathf.FloorToInt((worldZ + _half) / CellSize);
		return new Vector2I(x, z);
	}

	public Vector3 CellCenter(Vector2I cell)
	{
		return new Vector3(CellMin(cell.X) + CellSize * 0.5f, SurfaceY(cell), CellMin(cell.Y) + CellSize * 0.5f);
	}

	public float CellMin(int index)
	{
		return -_half + index * CellSize;
	}

	private void FillRect(int x0, int x1, int z0, int z1, TerrainCellKind kind, int level, TerrainRampDirection ramp)
	{
		for (var x = x0; x <= x1; x++)
		{
			for (var z = z0; z <= z1; z++)
			{
				Set(x, z, kind, level, ramp);
			}
		}
	}

	private void Set(int x, int z, TerrainCellKind kind, int level, TerrainRampDirection ramp)
	{
		if (x < 0 || z < 0 || x >= CellCount || z >= CellCount)
		{
			return;
		}

		var index = x + z * CellCount;
		_kinds[index] = kind;
		_levels[index] = level;
		_ramps[index] = ramp;
	}

	private int Index(Vector2I cell)
	{
		return cell.X + cell.Y * CellCount;
	}

	private static Vector2I StepOffset(TerrainRampDirection direction)
	{
		return direction switch
		{
			TerrainRampDirection.PositiveX => new Vector2I(1, 0),
			TerrainRampDirection.NegativeX => new Vector2I(-1, 0),
			TerrainRampDirection.PositiveZ => new Vector2I(0, 1),
			TerrainRampDirection.NegativeZ => new Vector2I(0, -1),
			_ => Vector2I.Zero,
		};
	}
}
