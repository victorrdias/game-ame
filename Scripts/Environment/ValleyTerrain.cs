using Godot;

namespace Vilanova;

/// <summary>
/// Handcrafted 240m valley: flat town, terraces, a hollow, and dirt ramps.
/// </summary>
public partial class ValleyTerrain : Node3D
{
	[Export] public float TerrainSize { get; set; } = TerrainHeightField.ValleySize;
	[Export] public float CellSize { get; set; } = 2.0f;

	public TerrainHeightField Field { get; private set; }

	private static readonly Color GrassLight = new("d5e68a");
	private static readonly Color GrassDeep = new("8ec85a");
	private static readonly Color ShelfLight = new("c6d46a");
	private static readonly Color ShelfDeep = new("7eae4c");
	private static readonly Color OliveLight = new("b7c46a");
	private static readonly Color OliveDeep = new("6f9848");
	private static readonly Color PlazaLight = new("f3d7a4");
	private static readonly Color PlazaDeep = new("e2b56e");
	private static readonly Color PathLight = new("e8c48a");
	private static readonly Color PathDeep = new("d4a15c");
	private static readonly Color RockA = new("c4b4a0");
	private static readonly Color RockB = new("a89078");
	private static readonly Color HollowA = new("6e9a62");
	private static readonly Color HollowB = new("5c8654");
	private static readonly Color Cliff = new("b8a090");
	private static readonly Color CliffDark = new("8d7260");
	public override void _Ready()
	{
		Field = TerrainHeightField.CreateValley(TerrainSize, CellSize);
		AddChild(BuildGroundMesh());
		AddChild(BuildCollision());
	}

	private MeshInstance3D BuildGroundMesh()
	{
		var surface = new SurfaceTool();
		surface.Begin(Mesh.PrimitiveType.Triangles);
		for (var x = 0; x < Field.CellCount; x++)
		{
			for (var z = 0; z < Field.CellCount; z++)
			{
				var cell = new Vector2I(x, z);
				var color = ColorFor(cell);
				var sw = Corner(x, z, 0, 0);
				var se = Corner(x, z, 1, 0);
				var nw = Corner(x, z, 0, 1);
				var ne = Corner(x, z, 1, 1);
				AddFace(surface, sw, se, ne, nw, Vector3.Up, color);

				AddPlayableWall(surface, cell, new Vector2I(1, 0), se, ne, color);
				AddPlayableWall(surface, cell, new Vector2I(-1, 0), nw, sw, color);
				AddPlayableWall(surface, cell, new Vector2I(0, 1), ne, nw, color);
				AddPlayableWall(surface, cell, new Vector2I(0, -1), sw, se, color);
			}
		}

		var material = new StandardMaterial3D
		{
			VertexColorUseAsAlbedo = true,
			Roughness = 1.0f,
			Metallic = 0.0f,
			SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
		};
		surface.SetMaterial(material);
		return new MeshInstance3D
		{
			Name = "ValleyMesh",
			Mesh = surface.Commit(),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
	}

	private StaticBody3D BuildCollision()
	{
		var body = new StaticBody3D
		{
			Name = "ValleyBody",
			CollisionLayer = CollisionLayers.Terrain,
			CollisionMask = 0,
		};

		var safety = new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = new Vector3(TerrainSize + 12.0f, 0.4f, TerrainSize + 12.0f) },
			Position = new Vector3(0.0f, -2.6f, 0.0f),
		};
		body.AddChild(safety);

		AddMergedTops(body);
		for (var x = 0; x < Field.CellCount; x++)
		{
			for (var z = 0; z < Field.CellCount; z++)
			{
				var cell = new Vector2I(x, z);
				if (Field.KindAt(cell) == TerrainCellKind.Ramp)
				{
					body.AddChild(RampShape(x, z));
				}

				AddWallShape(body, cell, new Vector2I(1, 0));
				AddWallShape(body, cell, new Vector2I(-1, 0));
				AddWallShape(body, cell, new Vector2I(0, 1));
				AddWallShape(body, cell, new Vector2I(0, -1));
			}
		}

		return body;
	}

	private void AddMergedTops(StaticBody3D body)
	{
		var count = Field.CellCount;
		var used = new bool[count, count];
		for (var x = 0; x < count; x++)
		{
			for (var z = 0; z < count; z++)
			{
				if (used[x, z] || Field.KindAt(new Vector2I(x, z)) == TerrainCellKind.Ramp)
				{
					continue;
				}

				var kind = Field.KindAt(new Vector2I(x, z));
				var level = Field.LevelAt(new Vector2I(x, z));
				var width = 1;
				while (x + width < count && CanMerge(used, x + width, z, kind, level))
				{
					width++;
				}

				var depth = 1;
				while (z + depth < count && RowMatches(used, x, z + depth, width, kind, level))
				{
					depth++;
				}

				for (var ix = 0; ix < width; ix++)
				{
					for (var iz = 0; iz < depth; iz++)
					{
						used[x + ix, z + iz] = true;
					}
				}

				var sizeX = width * CellSize;
				var sizeZ = depth * CellSize;
				var originX = Field.CellMin(x);
				var originZ = Field.CellMin(z);
				var center = new Vector3(originX + sizeX * 0.5f, level * Field.Step - 0.25f, originZ + sizeZ * 0.5f);
				body.AddChild(BoxShape(center, new Vector3(sizeX, 0.5f, sizeZ)));
			}
		}
	}

	private bool CanMerge(bool[,] used, int x, int z, TerrainCellKind kind, int level)
	{
		return !used[x, z]
			&& Field.KindAt(new Vector2I(x, z)) == kind
			&& Field.LevelAt(new Vector2I(x, z)) == level
			&& kind != TerrainCellKind.Ramp;
	}

	private bool RowMatches(bool[,] used, int x, int z, int width, TerrainCellKind kind, int level)
	{
		for (var ix = 0; ix < width; ix++)
		{
			if (!CanMerge(used, x + ix, z, kind, level))
			{
				return false;
			}
		}

		return true;
	}

	private CollisionShape3D RampShape(int x, int z)
	{
		var y00 = Field.CornerY(x, z, 0, 0);
		var y10 = Field.CornerY(x, z, 1, 0);
		var y01 = Field.CornerY(x, z, 0, 1);
		var y11 = Field.CornerY(x, z, 1, 1);
		var x0 = Field.CellMin(x);
		var x1 = x0 + CellSize;
		var z0 = Field.CellMin(z);
		var z1 = z0 + CellSize;
		var shape = new ConvexPolygonShape3D
		{
			Points = new[]
			{
				new Vector3(x0, y00 - 0.45f, z0),
				new Vector3(x1, y10 - 0.45f, z0),
				new Vector3(x0, y01 - 0.45f, z1),
				new Vector3(x1, y11 - 0.45f, z1),
				new Vector3(x0, y00, z0),
				new Vector3(x1, y10, z0),
				new Vector3(x0, y01, z1),
				new Vector3(x1, y11, z1),
			},
		};
		return new CollisionShape3D { Shape = shape };
	}

	private void AddWallShape(StaticBody3D body, Vector2I cell, Vector2I direction)
	{
		var neighbor = cell + direction;
		if (!TryEdgeRise(cell, neighbor, out var low, out var high))
		{
			return;
		}

		var rise = high - low;
		var alongZ = direction.X != 0;
		var boundary = direction.X > 0 || direction.Y > 0
			? Field.CellMin(alongZ ? cell.X + 1 : cell.Y + 1)
			: Field.CellMin(alongZ ? cell.X : cell.Y);
		var inward = alongZ ? new Vector3(-direction.X * 0.16f, 0.0f, 0.0f) : new Vector3(0.0f, 0.0f, -direction.Y * 0.16f);
		var center = Field.CellCenter(cell);
		var position = alongZ
			? new Vector3(boundary, (low + high) * 0.5f, center.Z)
			: new Vector3(center.X, (low + high) * 0.5f, boundary);
		position += inward;
		var size = alongZ
			? new Vector3(0.32f, rise + 0.2f, CellSize * 0.92f)
			: new Vector3(CellSize * 0.92f, rise + 0.2f, 0.32f);
		body.AddChild(BoxShape(position, size));
	}

	private void AddPlayableWall(SurfaceTool surface, Vector2I cell, Vector2I direction, Vector3 from, Vector3 to, Color topColor)
	{
		if (!Field.InBounds(cell + direction))
		{
			return;
		}

		AddEdgeWall(surface, cell, direction, from, to, topColor);
	}

	private void AddEdgeWall(SurfaceTool surface, Vector2I cell, Vector2I direction, Vector3 from, Vector3 to, Color topColor)
	{
		var neighbor = cell + direction;
		if (!TryEdgeRise(cell, neighbor, out var low, out _))
		{
			return;
		}

		var drop = Field.InBounds(neighbor) ? low : -2.2f;
		var bottomFrom = new Vector3(from.X, drop, from.Z);
		var bottomTo = new Vector3(to.X, drop, to.Z);
		var outward = new Vector3(direction.X, 0.0f, direction.Y);
		var face = Field.KindAt(cell) == TerrainCellKind.Rim ? CliffDark : Cliff;
		if (topColor == PathLight || topColor == PathDeep || topColor == PlazaLight || topColor == PlazaDeep)
		{
			face = PathDeep;
		}

		AddFace(surface, from, to, bottomTo, bottomFrom, outward, face);
	}

	private bool TryEdgeRise(Vector2I cell, Vector2I neighbor, out float low, out float high)
	{
		high = EdgeHeight(cell, neighbor);
		low = Field.InBounds(neighbor) ? EdgeHeight(neighbor, cell) : -2.2f;
		if (high <= low + 0.75f)
		{
			return false;
		}

		return !Field.InBounds(neighbor) || !Field.AllowsStep(cell, neighbor);
	}

	private float EdgeHeight(Vector2I cell, Vector2I toward)
	{
		var cornerX = toward.X > 0 ? 1 : 0;
		var cornerZ = toward.Y > 0 ? 1 : 0;
		if (toward.X != 0)
		{
			return (Field.CornerY(cell.X, cell.Y, cornerX, 0) + Field.CornerY(cell.X, cell.Y, cornerX, 1)) * 0.5f;
		}

		return (Field.CornerY(cell.X, cell.Y, 0, cornerZ) + Field.CornerY(cell.X, cell.Y, 1, cornerZ)) * 0.5f;
	}

	private Color ColorFor(Vector2I cell)
	{
		if (Field.KindAt(cell) == TerrainCellKind.Ramp)
		{
			return Checker(cell) ? PathLight : PathDeep;
		}

		if (IsPlaza(cell))
		{
			return Checker(cell) ? PlazaLight : PlazaDeep;
		}

		if (IsDirt(cell))
		{
			return Checker(cell) ? PathLight : PathDeep;
		}

		if (Field.KindAt(cell) == TerrainCellKind.Rim)
		{
			return Checker(cell) ? RockA : RockB;
		}

		var level = Field.LevelAt(cell);
		if (level >= 3)
		{
			return Checker(cell) ? RockA : RockB;
		}

		if (level == 2)
		{
			return Checker(cell) ? OliveLight : OliveDeep;
		}

		if (level == 1)
		{
			return Checker(cell) ? ShelfLight : ShelfDeep;
		}

		if (level < 0)
		{
			return Checker(cell) ? HollowA : HollowB;
		}

		return Checker(cell) ? GrassLight : GrassDeep;
	}

	private static bool Checker(Vector2I cell)
	{
		return ((cell.X / 2) + (cell.Y / 2)) % 2 == 0;
	}

	private static bool IsPlaza(Vector2I cell)
	{
		return cell.X is >= 57 and <= 63 && cell.Y is >= 64 and <= 70;
	}

	private static bool IsDirt(Vector2I cell)
	{
		return cell.X is >= 66 and <= 69 && cell.Y is >= 36 and <= 64;
	}

	private Vector3 Corner(int x, int z, int cornerX, int cornerZ)
	{
		return new Vector3(
			Field.CellMin(x) + cornerX * CellSize,
			Field.CornerY(x, z, cornerX, cornerZ),
			Field.CellMin(z) + cornerZ * CellSize);
	}

	private static CollisionShape3D BoxShape(Vector3 position, Vector3 size)
	{
		return new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = size },
			Position = position,
		};
	}

	private static void AddFace(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 expectedOutward, Color color)
	{
		AddTriangleFacing(surface, a, b, c, expectedOutward, color);
		AddTriangleFacing(surface, a, c, d, expectedOutward, color);
	}

	private static void AddTriangleFacing(
		SurfaceTool surface,
		Vector3 a,
		Vector3 b,
		Vector3 c,
		Vector3 expectedOutward,
		Color color)
	{
		var normal = (b - a).Cross(c - a);
		if (normal.LengthSquared() < 0.000001f)
		{
			return;
		}

		if (normal.Dot(expectedOutward) < 0.0f)
		{
			AddTriangle(surface, a, c, b, color);
			return;
		}

		AddTriangle(surface, a, b, c, color);
	}

	private static void AddTriangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Color color)
	{
		var normal = (b - a).Cross(c - a);
		if (normal.LengthSquared() < 0.000001f)
		{
			return;
		}

		// Godot 4 draws clockwise faces. The arguments stay in right-hand order.
		normal = normal.Normalized();
		surface.SetNormal(normal);
		surface.SetColor(color);
		surface.AddVertex(a);
		surface.SetNormal(normal);
		surface.SetColor(color);
		surface.AddVertex(c);
		surface.SetNormal(normal);
		surface.SetColor(color);
		surface.AddVertex(b);
	}
}
