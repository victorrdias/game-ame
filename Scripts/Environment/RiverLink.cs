using Godot;

namespace Vilanova;

/// <summary>
/// Hides the stone border on any side that touches another piece in the same link group.
/// </summary>
public partial class RiverLink : Node
{
	public void Apply(PlacedBuilding self, System.Func<Vector2I, PlacedBuilding> occupantAt, System.Func<Vector3, Vector2I> worldToCell)
	{
		var group = self.Definition.LinkGroup;
		if (string.IsNullOrEmpty(group))
		{
			return;
		}

		var root = GetParent() as Node3D;
		var model = root?.GetNodeOrNull<Node3D>("VisualRoot/BlenderModel");
		if (root == null || model == null)
		{
			return;
		}

		var cell = 2.0f / TownGridLayout.Subdivisions;
		var halfX = self.Definition.SizeInCells.X * cell * 0.5f;
		var halfZ = self.Definition.SizeInCells.Y * cell * 0.5f;
		HideTouchingBorders(model, root, self, group, occupantAt, worldToCell, halfX, halfZ, cell);
	}

	private static void HideTouchingBorders(
		Node node,
		Node3D root,
		PlacedBuilding self,
		string group,
		System.Func<Vector2I, PlacedBuilding> occupantAt,
		System.Func<Vector3, Vector2I> worldToCell,
		float halfX,
		float halfZ,
		float cell)
	{
		foreach (var child in node.GetChildren())
		{
			HideTouchingBorders(child, root, self, group, occupantAt, worldToCell, halfX, halfZ, cell);
		}

		if (node is not MeshInstance3D piece)
		{
			return;
		}

		var name = piece.Name.ToString();
		var positiveX = name.StartsWith("EdgePX");
		var negativeX = name.StartsWith("EdgeNX");
		var positiveZ = name.StartsWith("EdgePZ");
		var negativeZ = name.StartsWith("EdgeNZ");
		if (!positiveX && !negativeX && !positiveZ && !negativeZ)
		{
			return;
		}

		var center = piece.GlobalTransform * piece.GetAabb().GetCenter();
		var local = root.GlobalBasis.Inverse() * (center - root.GlobalPosition);
		Vector3 outside;
		if (positiveX || negativeX)
		{
			var sign = positiveX ? 1.0f : -1.0f;
			outside = new Vector3(sign * (halfX + cell * 0.5f), 0.0f, local.Z);
		}
		else
		{
			var sign = positiveZ ? 1.0f : -1.0f;
			outside = new Vector3(local.X, 0.0f, sign * (halfZ + cell * 0.5f));
		}

		var neighbor = occupantAt(worldToCell(root.GlobalPosition + root.GlobalBasis * outside));
		var connected = neighbor != null && neighbor != self && neighbor.Definition.LinkGroup == group;
		piece.Visible = !connected;
	}
}
