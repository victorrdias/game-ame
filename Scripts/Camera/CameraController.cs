using Godot;

namespace Vilanova;

/// <summary>
/// Isometric orthographic camera: follows target, continuous Q/E yaw, scroll zoom.
/// </summary>
public partial class CameraController : Node3D
{
	[Export] public NodePath FollowTargetPath { get; set; } = new("../Player");
	[Export] public float FollowSmoothness { get; set; } = 10.0f;
	[Export] public float RotateSpeedDegrees { get; set; } = 95.0f;
	[Export] public float ZoomSmoothness { get; set; } = 14.0f;
	[Export] public float ZoomStep { get; set; } = 2.0f;
	[Export] public float MinZoom { get; set; } = 10.0f;
	[Export] public float MaxZoom { get; set; } = 36.0f;

	public float CurrentYawDegrees => _currentYawDegrees;

	/// <summary>
	/// Ground axes matching the camera: forward is where the lens looks, right is screen-right.
	/// </summary>
	public void GetPlanarMoveAxes(out Vector3 forward, out Vector3 right)
	{
		var basis = _camera.GlobalTransform.Basis;
		var look = -basis.Z;
		look.Y = 0.0f;
		forward = look.LengthSquared() > 0.0001f ? look.Normalized() : new Vector3(0.0f, 0.0f, -1.0f);

		var side = basis.X;
		side.Y = 0.0f;
		right = side.LengthSquared() > 0.0001f ? side.Normalized() : new Vector3(1.0f, 0.0f, 0.0f);
	}

	private Node3D _followTarget = null!;
	private Node3D _pivot = null!;
	private Camera3D _camera = null!;

	private Vector3 _targetFocus;
	private float _currentYawDegrees;
	private float _targetZoom;
	private float _currentZoom;

	public override void _Ready()
	{
		_followTarget = GetNode<Node3D>(FollowTargetPath);
		_pivot = GetNode<Node3D>("Pivot");
		_camera = _pivot.GetNode<Camera3D>("Camera3D");
		_camera.Projection = Camera3D.ProjectionType.Orthogonal;
		_camera.Position = new Vector3(20.0f, 20.0f, 20.0f);
		_camera.LookAt(Vector3.Zero, Vector3.Up);

		var focus = _followTarget.GlobalPosition;
		_targetFocus = new Vector3(focus.X, 0.0f, focus.Z);
		GlobalPosition = _targetFocus;
		_currentYawDegrees = _pivot.RotationDegrees.Y;
		_currentZoom = _targetZoom = _camera.Size;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
		{
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelUp)
		{
			_targetZoom = Mathf.Clamp(_targetZoom - ZoomStep, MinZoom, MaxZoom);
		}
		else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
		{
			_targetZoom = Mathf.Clamp(_targetZoom + ZoomStep, MinZoom, MaxZoom);
		}
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		UpdateFollowTarget();
		HandleContinuousRotation(dt);

		var smoothed = GlobalPosition.Lerp(_targetFocus, SmoothFactor(FollowSmoothness, dt));
		GlobalPosition = new Vector3(smoothed.X, 0.0f, smoothed.Z);

		_pivot.RotationDegrees = new Vector3(_pivot.RotationDegrees.X, _currentYawDegrees, 0);

		_currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, SmoothFactor(ZoomSmoothness, dt));
		_camera.Size = _currentZoom;
	}

	private void UpdateFollowTarget()
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return;
		}

		var pos = _followTarget.GlobalPosition;
		_targetFocus = new Vector3(pos.X, 0.0f, pos.Z);
	}

	private void HandleContinuousRotation(float dt)
	{
		var rotationInput = 0.0f;
		if (Input.IsActionPressed("cam_rotate_left"))
		{
			rotationInput -= 1.0f;
		}

		if (Input.IsActionPressed("cam_rotate_right"))
		{
			rotationInput += 1.0f;
		}

		if (Mathf.IsZeroApprox(rotationInput))
		{
			return;
		}

		_currentYawDegrees += rotationInput * RotateSpeedDegrees * dt;
	}

	private static float SmoothFactor(float smoothness, float delta)
	{
		return 1.0f - Mathf.Exp(-smoothness * delta);
	}
}
