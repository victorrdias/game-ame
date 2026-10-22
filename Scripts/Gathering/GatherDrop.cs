using Godot;

namespace Vilanova;

/// <summary>
/// Visual chunk on the ground. No collision, so residents and the player walk through it.
/// </summary>
public partial class GatherDrop : Node3D
{
	public const string GroupName = "world_drop";

	private static readonly Vector3[] WoodOffsets =
	{
		new(0.48f, 0.0f, 0.18f),
		new(-0.36f, 0.0f, 0.42f),
		new(0.12f, 0.0f, -0.5f),
		new(-0.2f, 0.0f, -0.28f),
	};

	private static readonly Vector3[] StoneOffsets =
	{
		new(0.4f, 0.0f, -0.16f),
		new(-0.32f, 0.0f, 0.34f),
		new(0.08f, 0.0f, 0.46f),
	};

	private readonly WorldDropStack _stack = new();
	private bool _settled;

	public string ItemId => _stack.ItemId;
	public int Quantity => _stack.Quantity;
	public WorldDropState State => _stack.State;
	public bool Yielded => _stack.Yielded;
	public bool Settled => _settled;
	public double SpawnedAt { get; private set; }

	public static void Spawn(Node3D parent, TerrainHeightField heights, Vector3 origin, GatherKind kind, int count)
	{
		if (parent == null)
		{
			return;
		}

		var offsets = kind == GatherKind.Stone ? StoneOffsets : WoodOffsets;
		var total = Mathf.Clamp(count, 1, offsets.Length);
		var itemId = ItemIds.ForGatherKind(kind);
		for (var i = 0; i < total; i++)
		{
			var drop = new GatherDrop();
			parent.AddChild(drop);
			drop.Configure(itemId, 1);
			drop.BuildMesh(kind, i);
			var landing = origin + offsets[i];
			landing.Y = (heights?.SampleY(landing.X, landing.Z) ?? origin.Y) + 0.06f;
			var start = origin + new Vector3(0, 0.7f, 0);
			drop.GlobalPosition = start;
			var mid = (start + landing) * 0.5f + new Vector3(0, 0.55f, 0);
			var tween = drop.CreateTween();
			tween.TweenProperty(drop, "global_position", mid, 0.16f);
			tween.TweenProperty(drop, "global_position", landing, 0.2f);
			tween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(drop))
				{
					drop.MarkSettled();
				}
			}));
		}
	}

	public void Configure(string itemId, int quantity)
	{
		_stack.ItemId = itemId ?? string.Empty;
		_stack.Quantity = quantity < 1 ? 1 : quantity;
		_stack.State = WorldDropState.Available;
		SpawnedAt = Time.GetTicksMsec() / 1000.0;
		AddToGroup(GroupName);
	}

	public bool IsReady(double now, double pickupDelay)
	{
		if (_stack.State == WorldDropState.Collected)
		{
			return false;
		}

		var age = now - SpawnedAt;
		if (age < pickupDelay)
		{
			return false;
		}

		return _settled || age >= 1.0;
	}

	public void MarkSettled()
	{
		_settled = true;
	}

	public void SetAttracted(bool attracted)
	{
		if (_stack.State == WorldDropState.Collected)
		{
			return;
		}

		_stack.State = attracted ? WorldDropState.Attracted : WorldDropState.Available;
	}

	public void ReleaseYield()
	{
		_stack.ReleaseYield();
	}

	public void Hold()
	{
		if (_stack.State == WorldDropState.Collected)
		{
			return;
		}

		_stack.State = WorldDropState.Available;
		_stack.Yielded = true;
	}

	public int Take(int accepted)
	{
		var taken = _stack.Take(accepted);
		if (_stack.State == WorldDropState.Collected)
		{
			RemoveFromGroup(GroupName);
			QueueFree();
		}

		return taken;
	}

	private void BuildMesh(GatherKind kind, int index)
	{
		MeshInstance3D mesh;
		if (kind == GatherKind.Stone)
		{
			mesh = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.2f, 0.14f, 0.16f) },
				RotationDegrees = new Vector3(8, 20 * index, 12),
			};
			mesh.MaterialOverride = Matte(new Color("b8a898"));
		}
		else
		{
			mesh = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.34f, 0.1f, 0.1f) },
				RotationDegrees = new Vector3(0, 40 * index, 90),
			};
			mesh.MaterialOverride = Matte(index % 2 == 0 ? new Color("9c6b43") : new Color("6f4a2e"));
		}

		AddChild(mesh);
	}

	private static StandardMaterial3D Matte(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = 1.0f,
			Metallic = 0.0f,
			SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
		};
	}
}
