using Godot;

namespace Vilanova;

public partial class PlayerController : CharacterBody3D
{
	[Export] public float MoveSpeed { get; set; } = 5.5f;
	[Export] public float RotationSmoothness { get; set; } = 14.0f;
	[Export] public NodePath CameraControllerPath { get; set; } = new("../CameraController");

	private Node3D _visualRoot = null!;
	private CameraController _cameraController = null!;
	private float _visualYaw;
	private float _forcedYaw;
	private ulong _forcedYawUntilMs;
	private bool _leisureHold;
	private Vector3 _leisurePosition;
	private float _leisureYaw;

	public Node3D VisualRoot => _visualRoot;
	public bool HasMoveInput => ReadMoveInput() != Vector2.Zero;

	public void SetLeisureHold(Vector3 worldPosition, float yawRadians)
	{
		_leisureHold = true;
		_leisurePosition = worldPosition;
		_leisureYaw = yawRadians;
		_forcedYawUntilMs = 0;
		GlobalPosition = worldPosition;
		Velocity = Vector3.Zero;
		_visualYaw = yawRadians;
		_visualRoot.Basis = new Basis(Vector3.Up, yawRadians);
	}

	public void ClearLeisureHold()
	{
		_leisureHold = false;
		Velocity = Vector3.Zero;
	}

	public void FaceTowards(Vector3 worldPoint, float seconds)
	{
		var flat = worldPoint - GlobalPosition;
		flat.Y = 0.0f;
		if (flat.LengthSquared() <= 0.0001f)
		{
			return;
		}

		flat = flat.Normalized();
		_forcedYaw = Mathf.Atan2(flat.X, flat.Z);
		_forcedYawUntilMs = Time.GetTicksMsec() + (ulong)(seconds * 1000.0f);
	}

	public override void _Ready()
	{
		_cameraController = GetNode<CameraController>(CameraControllerPath);
		_visualRoot = GetNode<Node3D>("VisualRoot");
		_visualRoot.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
		CollisionLayer = CollisionLayers.Player;
		CollisionMask = CollisionLayers.PlayerMask;
		FloorSnapLength = 0.45f;
		FloorMaxAngle = Mathf.DegToRad(50.0f);
		var leisure = new PlayerLeisure { Name = "Leisure" };
		AddChild(leisure);
	}

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;
		if (_leisureHold)
		{
			Velocity = Vector3.Zero;
			GlobalPosition = _leisurePosition;
			_visualYaw = _leisureYaw;
			_visualRoot.Basis = new Basis(Vector3.Up, _visualYaw);
			return;
		}

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

		var forced = Time.GetTicksMsec() < _forcedYawUntilMs;
		if (!forced && horizontal == Vector3.Zero)
		{
			return;
		}

		var targetYaw = _forcedYaw;
		if (!forced)
		{
			var directionFlat = new Vector3(horizontal.X, 0.0f, horizontal.Z);
			if (directionFlat.LengthSquared() <= 0.0001f)
			{
				return;
			}

			directionFlat = directionFlat.Normalized();
			targetYaw = Mathf.Atan2(directionFlat.X, directionFlat.Z);
		}

		_visualYaw = Mathf.LerpAngle(_visualYaw, targetYaw, SmoothFactor(RotationSmoothness, dt));
		_visualRoot.Basis = new Basis(Vector3.Up, _visualYaw);
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
