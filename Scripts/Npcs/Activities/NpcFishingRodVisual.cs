using Godot;

namespace Vilanova;

/// <summary>
/// Procedural rod, line segment, and bobber for NPC fishing. No physics on the line.
/// </summary>
public partial class NpcFishingRodVisual : Node3D
{
	private Node3D _rodRoot = null!;
	private MeshInstance3D _lineMesh = null!;
	private MeshInstance3D _bobber = null!;
	private Vector3 _castTarget;
	private float _castProgress;
	private float _idlePhase;
	private bool _lineOut;

	public float CastAmount => _castProgress;
	public float IdlePhase => _idlePhase;

	public override void _Ready()
	{
		_rodRoot = BuildRod();
		AddChild(_rodRoot);
		_lineMesh = BuildLine();
		AddChild(_lineMesh);
		_bobber = BuildBobber();
		AddChild(_bobber);
		Visible = false;
	}

	public void ShowRod()
	{
		Visible = true;
		_lineOut = false;
		_castProgress = 0.0f;
		_rodRoot.RotationDegrees = new Vector3(-12.0f, 0.0f, 8.0f);
		_lineMesh.Visible = false;
		_bobber.Visible = false;
	}

	public void HideRod()
	{
		Visible = false;
		_lineOut = false;
		_lineMesh.Visible = false;
		_bobber.Visible = false;
	}

	public void BeginCast(Vector3 waterTarget)
	{
		_castTarget = waterTarget;
		_castProgress = 0.0f;
		_lineOut = false;
		_lineMesh.Visible = false;
		_bobber.Visible = false;
	}

	public bool TickCast(float dt)
	{
		_castProgress += dt / 0.55f;
		var t = Mathf.Clamp(_castProgress, 0.0f, 1.0f);
		var swing = Mathf.Lerp(-18.0f, 62.0f, t * t);
		_rodRoot.RotationDegrees = new Vector3(swing, 0.0f, 8.0f);
		if (t < 1.0f)
		{
			return false;
		}

		_lineOut = true;
		_lineMesh.Visible = true;
		_bobber.Visible = true;
		UpdateLineAndBobber(0.0f);
		return true;
	}

	public void TickIdle(float dt)
	{
		if (!_lineOut)
		{
			return;
		}

		_idlePhase += dt;
		UpdateLineAndBobber(_idlePhase);
		var wobble = Mathf.Sin(_idlePhase * 2.4f) * 4.0f;
		_rodRoot.RotationDegrees = new Vector3(48.0f + wobble, 0.0f, 8.0f);
	}

	public void BeginReel()
	{
		_lineOut = false;
		_lineMesh.Visible = false;
		_bobber.Visible = false;
		_rodRoot.RotationDegrees = new Vector3(-12.0f, 0.0f, 8.0f);
	}

	public bool TickReel(float dt)
	{
		_castProgress -= dt / 0.45f;
		var t = Mathf.Clamp(_castProgress, 0.0f, 1.0f);
		var swing = Mathf.Lerp(-18.0f, 38.0f, t);
		_rodRoot.RotationDegrees = new Vector3(swing, 0.0f, 8.0f);
		return t <= 0.01f;
	}

	private void UpdateLineAndBobber(float phase)
	{
		var tip = _rodRoot.GlobalTransform * new Vector3(0.0f, 0.52f, 0.0f);
		var bob = _castTarget;
		bob.Y += Mathf.Sin(phase * 3.1f) * 0.035f;
		_bobber.GlobalPosition = bob;

		var length = tip.DistanceTo(bob);
		if (length < 0.08f)
		{
			_lineMesh.Visible = false;
			return;
		}

		_lineMesh.Visible = true;
		var direction = (bob - tip) / length;
		var up = Mathf.Abs(direction.Dot(Vector3.Up)) > 0.96f ? Vector3.Right : Vector3.Up;
		_lineMesh.GlobalPosition = (tip + bob) * 0.5f;
		_lineMesh.LookAt(bob, up);
		_lineMesh.RotateObjectLocal(Vector3.Right, Mathf.Pi * 0.5f);
		_lineMesh.Scale = new Vector3(1.0f, length, 1.0f);
	}

	private static Node3D BuildRod()
	{
		var root = new Node3D { Position = new Vector3(-0.16f, 0.92f, 0.12f) };
		root.AddChild(Bar(new Vector3(0.04f, 0.5f, 0.04f), new Vector3(0, 0.25f, 0), new Color("6f4a2e")));
		root.AddChild(Bar(new Vector3(0.03f, 0.12f, 0.03f), new Vector3(0, 0.52f, 0), new Color("4a3528")));
		return root;
	}

	private static MeshInstance3D BuildLine()
	{
		return new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 1.0f },
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color("d8d4c8"),
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			},
		};
	}

	private static MeshInstance3D BuildBobber()
	{
		return new MeshInstance3D
		{
			Mesh = new SphereMesh { Radius = 0.06f, Height = 0.1f },
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color("c94a4a"),
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			},
		};
	}

	private static MeshInstance3D Bar(Vector3 size, Vector3 position, Color color)
	{
		return new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size },
			Position = position,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = color,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			},
		};
	}
}
