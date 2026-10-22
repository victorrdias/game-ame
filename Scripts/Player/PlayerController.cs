using Godot;

namespace Vilanova;

public partial class PlayerController : CharacterBody3D
{
	[Export] public float MoveSpeed { get; set; } = 5.5f;
	[Export] public float RotationSmoothness { get; set; } = 14.0f;
	[Export] public NodePath CameraControllerPath { get; set; } = new("../CameraController");

	private Node3D _visualRoot = null!;
	private CameraController _cameraController = null!;
	private float _visualYawDegrees;

	public override void _Ready()
	{
		_cameraController = GetNode<CameraController>(CameraControllerPath);
		_visualRoot = GetNode<Node3D>("VisualRoot");
		CollisionLayer = CollisionLayers.Player;
		CollisionMask = CollisionLayers.PlayerMask;
	}

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;
		var input = ReadMoveInput();
		Vector3 horizontal = Vector3.Zero;
		if (input != Vector2.Zero)
		{
			input = input.Normalized();
			_cameraController.GetPlanarMoveAxes(out var forward, out var right);
			var direction = (forward * input.Y + right * input.X).Normalized();
			horizontal = direction * MoveSpeed;
		}

		var velocityY = Velocity.Y;
		if (!IsOnFloor())
		{
			velocityY += (float)GetGravity().Y * dt;
		}
		else if (velocityY < 0.0f)
		{
			velocityY = 0.0f;
		}

		Velocity = new Vector3(horizontal.X, velocityY, horizontal.Z);
		MoveAndSlide();

		if (horizontal == Vector3.Zero)
		{
			return;
		}

		var directionFlat = new Vector3(Velocity.X, 0.0f, Velocity.Z);
		if (directionFlat.LengthSquared() <= 0.0001f)
		{
			return;
		}

		directionFlat = directionFlat.Normalized();

		var targetYaw = Mathf.RadToDeg(Mathf.Atan2(directionFlat.X, directionFlat.Z));
		_visualYawDegrees = Mathf.LerpAngle(_visualYawDegrees, targetYaw, SmoothFactor(RotationSmoothness, dt));
		_visualRoot.RotationDegrees = new Vector3(0, _visualYawDegrees, 0);
	}

	public Aabb GetOccupancyBounds()
	{
		var shape = GetNode<CollisionShape3D>("CollisionShape3D").Shape as CapsuleShape3D;
		var radius = shape?.Radius ?? 0.28f;
		var height = shape?.Height ?? 0.9f;
		var center = GlobalPosition + new Vector3(0, height * 0.5f + (shape?.Radius ?? 0.28f), 0);
		var size = new Vector3(radius * 2.2f, height + radius * 2.0f, radius * 2.2f);
		return new Aabb(center - size * 0.5f, size);
	}

	private static Vector2 ReadMoveInput()
	{
		var input = Vector2.Zero;
		if (Input.IsActionPressed("cam_move_forward"))
		{
			input.Y += 1.0f;
		}

		if (Input.IsActionPressed("cam_move_back"))
		{
			input.Y -= 1.0f;
		}

		if (Input.IsActionPressed("cam_move_left"))
		{
			input.X -= 1.0f;
		}

		if (Input.IsActionPressed("cam_move_right"))
		{
			input.X += 1.0f;
		}

		return input;
	}

	private static float SmoothFactor(float smoothness, float delta)
	{
		return 1.0f - Mathf.Exp(-smoothness * delta);
	}
}
