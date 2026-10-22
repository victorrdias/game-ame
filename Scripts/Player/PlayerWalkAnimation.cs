using Godot;

namespace Vilanova;

/// <summary>
/// Swings the block limbs while the body is moving. The capsule and the walk direction stay put.
/// </summary>
public partial class PlayerWalkAnimation : Node
{
	[Export] public float StrideLength { get; set; } = 1.25f;
	[Export] public float LegSwingDegrees { get; set; } = 32.0f;
	[Export] public float ArmSwingDegrees { get; set; } = 24.0f;
	[Export] public float BobHeight { get; set; } = 0.045f;
	[Export] public float BodyRollDegrees { get; set; } = 3.5f;
	public float StartPhase { get; set; }
	public float LocomotionBlend { get; set; } = 1.0f;

	private CharacterBody3D _body = null!;
	private Node3D _visual = null!;
	private Node3D _model = null!;
	private Node3D _legL;
	private Node3D _legR;
	private Node3D _armL;
	private Node3D _armR;
	private float _phase;
	private float _weight;

	public override void _Ready()
	{
		_visual = GetParent<Node3D>();
		_body = _visual.GetParent<CharacterBody3D>();
		_model = _visual.GetNodeOrNull<Node3D>("BlenderModel");
		if (_model == null)
		{
			return;
		}

		_legL = FindPart(_model, "LegL");
		_legR = FindPart(_model, "LegR");
		_armL = FindPart(_model, "ArmL");
		_armR = FindPart(_model, "ArmR");
		_phase = StartPhase;
	}

	public override void _Process(double delta)
	{
		if (_model == null || _body == null)
		{
			return;
		}

		if (LocomotionBlend <= 0.001f)
		{
			_weight = 0.0f;
			return;
		}

		var dt = (float)delta;
		var velocity = _body.Velocity;
		var speed = new Vector2(velocity.X, velocity.Z).Length();
		var moving = speed > 0.2f;
		var target = moving ? LocomotionBlend : 0.0f;
		_weight = Mathf.MoveToward(_weight, target, dt * 7.0f);
		if (moving)
		{
			_phase += speed / StrideLength * Mathf.Tau * dt;
		}

		var swing = Mathf.Sin(_phase) * _weight;
		// A hanging limb extends along local -Y. Positive X rotation carries it toward the face (local -Z).
		SetSwing(_legL, swing * LegSwingDegrees);
		SetSwing(_legR, -swing * LegSwingDegrees);
		SetSwing(_armL, -swing * ArmSwingDegrees);
		SetSwing(_armR, swing * ArmSwingDegrees);

		var bob = (1.0f - Mathf.Cos(_phase * 2.0f)) * 0.5f * BobHeight * _weight;
		var rest = _visual.Position;
		_visual.Position = new Vector3(rest.X, bob, rest.Z);
		_model.RotationDegrees = new Vector3(-2.5f * _weight, 180.0f, Mathf.Sin(_phase) * BodyRollDegrees * _weight);
	}

	private static void SetSwing(Node3D limb, float degrees)
	{
		if (limb == null)
		{
			return;
		}

		limb.RotationDegrees = new Vector3(degrees, 0.0f, 0.0f);
	}

	private static Node3D FindPart(Node node, string name)
	{
		foreach (var child in node.GetChildren())
		{
			if (child.Name.ToString().StartsWith(name))
			{
				return child as Node3D;
			}

			var nested = FindPart(child, name);
			if (nested != null)
			{
				return nested;
			}
		}

		return null;
	}
}
