using Godot;

namespace Vilanova;

/// <summary>
/// Pulls settled drops on the XZ plane and adds them to the inventory only after they fit.
/// </summary>
public partial class PlayerPickupCollector : Node
{
	[Export] public float AttractionRadius { get; set; } = 2.0f;
	[Export] public float CollectionRadius { get; set; } = 0.4f;
	[Export] public float AttractionSpeed { get; set; } = 5.5f;
	[Export] public float PickupDelay { get; set; } = 0.45f;
	[Export] public float VerticalLimit { get; set; } = 1.75f;
	[Export] public NodePath InventoryPath { get; set; } = new("../Inventory");

	private InventoryComponent _inventory = null!;
	private Node3D _body = null!;

	public override void _Ready()
	{
		_inventory = GetNode<InventoryComponent>(InventoryPath);
		_body = GetParent<Node3D>();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_body == null || _inventory == null)
		{
			return;
		}

		var now = Time.GetTicksMsec() / 1000.0;
		var space = _body.GetWorld3D()?.DirectSpaceState;
		foreach (var node in GetTree().GetNodesInGroup(GatherDrop.GroupName))
		{
			if (node is not GatherDrop drop || !GodotObject.IsInstanceValid(drop))
			{
				continue;
			}

			if (drop.State == WorldDropState.Collected)
			{
				continue;
			}

			var offset = drop.GlobalPosition - _body.GlobalPosition;
			var horizontal = Mathf.Sqrt(offset.X * offset.X + offset.Z * offset.Z);
			if (horizontal > AttractionRadius)
			{
				drop.ReleaseYield();
				if (drop.State == WorldDropState.Attracted)
				{
					drop.SetAttracted(false);
				}

				continue;
			}

			if (!PickupRules.CanAttract(
				    horizontal,
				    offset.Y,
				    drop.IsReady(now, PickupDelay),
				    now - drop.SpawnedAt,
				    PickupDelay,
				    AttractionRadius,
				    VerticalLimit,
				    drop.Yielded))
			{
				continue;
			}

			if (WallBlocks(space, drop))
			{
				if (drop.State == WorldDropState.Attracted)
				{
					drop.SetAttracted(false);
				}

				continue;
			}

			if (PickupRules.CanCollect(horizontal, CollectionRadius))
			{
				Collect(drop);
				continue;
			}

			drop.SetAttracted(true);
			var flat = new Vector3(offset.X, 0.0f, offset.Z);
			if (flat.LengthSquared() <= 0.0001f)
			{
				continue;
			}

			var step = AttractionSpeed * (float)delta;
			var next = drop.GlobalPosition - flat.Normalized() * step;
			next.Y = Mathf.MoveToward(drop.GlobalPosition.Y, _body.GlobalPosition.Y + 0.2f, step);
			drop.GlobalPosition = next;
		}
	}

	private void Collect(GatherDrop drop)
	{
		var added = _inventory.TryAddItem(drop.ItemId, drop.Quantity);
		if (added <= 0)
		{
			drop.Hold();
			return;
		}

		PickupFeedback.Show(_body, drop.GlobalPosition, $"+{added} {_inventory.DisplayNameOf(drop.ItemId)}");
		drop.Take(added);
	}

	private bool WallBlocks(PhysicsDirectSpaceState3D space, GatherDrop drop)
	{
		if (space == null)
		{
			return false;
		}

		var from = _body.GlobalPosition + new Vector3(0.0f, 0.7f, 0.0f);
		var to = drop.GlobalPosition + new Vector3(0.0f, 0.2f, 0.0f);
		var query = PhysicsRayQueryParameters3D.Create(from, to, CollisionLayers.Structure);
		query.CollideWithAreas = false;
		if (_body is CollisionObject3D body)
		{
			query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };
		}

		return space.IntersectRay(query).Count > 0;
	}
}
