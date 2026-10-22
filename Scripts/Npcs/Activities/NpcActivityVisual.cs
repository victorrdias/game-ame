using Godot;

namespace Vilanova;

/// <summary>
/// Procedural sit and fish poses on the shared blocky character. Does not drive locomotion.
/// </summary>
public partial class NpcActivityVisual : Node
{
	private Node3D _visualRoot = null!;
	private Node3D _model = null!;
	private Node3D _legL;
	private Node3D _legR;
	private Node3D _armL;
	private Node3D _armR;
	private PlayerWalkAnimation _walk;
	private NpcFishingRodVisual _rod = null!;
	private Tween _poseTween;

	private float _baseVisualY;
	private Vector3 _modelRestRotation;
	private float _sitPhase;
	private bool _sitting;
	private bool _active;

	private const float SitDrop = 0.24f;
	private const float SitLegDegrees = 78.0f;
	private const float SitArmDegrees = 34.0f;
	private const float SitLeanDegrees = -8.0f;

	public bool IsAnimating { get; private set; }

	public override void _Ready()
	{
		_visualRoot = GetParent<Node3D>();
		_model = _visualRoot.GetNodeOrNull<Node3D>("BlenderModel");
		_walk = _visualRoot.GetNodeOrNull<PlayerWalkAnimation>("Walk");
		if (_model != null)
		{
			_legL = FindPart(_model, "LegL");
			_legR = FindPart(_model, "LegR");
			_armL = FindPart(_model, "ArmL");
			_armR = FindPart(_model, "ArmR");
			_modelRestRotation = _model.RotationDegrees;
		}

		_baseVisualY = _visualRoot.Position.Y;
		_rod = new NpcFishingRodVisual { Name = "FishingRod" };
		_visualRoot.AddChild(_rod);
		_rod.Visible = false;
	}

	public void SetLocomotionEnabled(bool enabled)
	{
		if (_walk != null)
		{
			_walk.LocomotionBlend = enabled ? 1.0f : 0.0f;
		}
	}

	public void BeginSitDown(System.Action onComplete)
	{
		_active = true;
		_sitting = false;
		_sitPhase = 0.0f;
		IsAnimating = true;
		SetLocomotionEnabled(false);
		KillTween();
		_poseTween = CreateTween();
		_poseTween.SetParallel(true);
		_poseTween.TweenProperty(_visualRoot, "position:y", _baseVisualY - SitDrop, 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		if (_model != null)
		{
			_poseTween.TweenProperty(_model, "rotation_degrees:x", SitLeanDegrees, 0.5f);
		}

		TweenLegs(SitLegDegrees, 0.5f);
		TweenArms(SitArmDegrees, SitArmDegrees, 0.5f);
		_poseTween.Chain().TweenCallback(Callable.From(() =>
		{
			_sitting = true;
			IsAnimating = false;
			onComplete?.Invoke();
		}));
	}

	public void TickSitIdle(float dt)
	{
		if (!_sitting)
		{
			return;
		}

		_sitPhase += dt;
		var breath = Mathf.Sin(_sitPhase * 1.5f);
		var position = _visualRoot.Position;
		_visualRoot.Position = new Vector3(position.X, _baseVisualY - SitDrop + breath * 0.012f, position.Z);
		if (_model != null)
		{
			_model.RotationDegrees = new Vector3(SitLeanDegrees + breath * 1.2f, _modelRestRotation.Y, breath * 0.8f);
		}

		SetSwing(_legL, SitLegDegrees + breath * 1.4f);
		SetSwing(_legR, SitLegDegrees - breath * 1.4f);
		SetSwing(_armL, SitArmDegrees + breath * 2.0f);
		SetSwing(_armR, SitArmDegrees - breath * 2.0f);
	}

	public void BeginStandUp(System.Action onComplete)
	{
		_sitting = false;
		IsAnimating = true;
		KillTween();
		_poseTween = CreateTween();
		_poseTween.SetParallel(true);
		_poseTween.TweenProperty(_visualRoot, "position:y", _baseVisualY, 0.5f).SetTrans(Tween.TransitionType.Sine);
		if (_model != null)
		{
			_poseTween.TweenProperty(_model, "rotation_degrees", _modelRestRotation, 0.5f);
		}

		TweenLegs(0.0f, 0.5f);
		TweenArms(0.0f, 0.0f, 0.5f);
		_poseTween.Chain().TweenCallback(Callable.From(() =>
		{
			ResetPoseImmediate();
			_active = false;
			IsAnimating = false;
			SetLocomotionEnabled(true);
			onComplete?.Invoke();
		}));
	}

	public void BeginFishCast(Vector3 waterTarget, System.Action onCastComplete)
	{
		_active = true;
		_sitting = false;
		IsAnimating = true;
		SetLocomotionEnabled(false);
		_rod.ShowRod();
		_rod.BeginCast(waterTarget);
		PoseFishingArms(0.0f, 0.0f);
	}

	public bool TickFishCast(float dt)
	{
		var finished = _rod.TickCast(dt);
		var amount = Mathf.Clamp(_rod.CastAmount, 0.0f, 1.0f);
		PoseFishingArms(amount * amount, 0.0f);
		if (finished)
		{
			IsAnimating = false;
		}

		return finished;
	}

	public void TickFishIdle(float dt)
	{
		_rod.TickIdle(dt);
		PoseFishingArms(1.0f, _rod.IdlePhase);
	}

	public void BeginFishReel(System.Action onComplete)
	{
		IsAnimating = true;
		_rod.BeginReel();
	}

	public bool TickFishReel(float dt)
	{
		_rod.TickReel(dt);
		var amount = Mathf.Clamp(_rod.CastAmount, 0.0f, 1.0f);
		PoseFishingArms(amount, 0.0f);
		if (amount <= 0.01f)
		{
			_rod.HideRod();
			SetSwing(_armL, 0.0f);
			SetSwing(_armR, 0.0f);
			_active = false;
			IsAnimating = false;
			SetLocomotionEnabled(true);
			return true;
		}

		return false;
	}

	public void CancelImmediate()
	{
		KillTween();
		_rod.HideRod();
		_sitting = false;
		ResetPoseImmediate();
		_active = false;
		IsAnimating = false;
		SetLocomotionEnabled(true);
	}

	private void PoseFishingArms(float cast, float idlePhase)
	{
		var wobble = Mathf.Sin(idlePhase * 2.4f) * 5.0f * cast;
		SetSwing(_armR, Mathf.Lerp(-28.0f, 62.0f, cast) + wobble);
		SetSwing(_armL, Mathf.Lerp(6.0f, 18.0f, cast));
		if (_model != null && _active)
		{
			_model.RotationDegrees = new Vector3(-4.0f * cast, _modelRestRotation.Y, 0.0f);
		}
	}

	private void ResetPoseImmediate()
	{
		_visualRoot.Position = new Vector3(_visualRoot.Position.X, _baseVisualY, _visualRoot.Position.Z);
		if (_model != null)
		{
			_model.RotationDegrees = _modelRestRotation;
		}

		SetSwing(_legL, 0.0f);
		SetSwing(_legR, 0.0f);
		SetSwing(_armL, 0.0f);
		SetSwing(_armR, 0.0f);
	}

	private void TweenLegs(float degrees, float duration)
	{
		if (_legL != null)
		{
			_poseTween.TweenProperty(_legL, "rotation_degrees:x", degrees, duration);
		}

		if (_legR != null)
		{
			_poseTween.TweenProperty(_legR, "rotation_degrees:x", degrees, duration);
		}
	}

	private void TweenArms(float leftDegrees, float rightDegrees, float duration)
	{
		if (_armL != null)
		{
			_poseTween.TweenProperty(_armL, "rotation_degrees:x", leftDegrees, duration);
		}

		if (_armR != null)
		{
			_poseTween.TweenProperty(_armR, "rotation_degrees:x", rightDegrees, duration);
		}
	}

	private void KillTween()
	{
		if (_poseTween != null && _poseTween.IsValid())
		{
			_poseTween.Kill();
		}

		_poseTween = null;
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
