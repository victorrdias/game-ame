using Godot;

namespace Vilanova;

public static class BuildingVisualSpawnAnimation
{
	private const string VisualRootName = "VisualRoot";

	public static void Play(Node3D buildingRoot, float durationSeconds = 0.32f)
	{
		var visualRoot = buildingRoot.GetNodeOrNull<Node3D>(VisualRootName);
		if (visualRoot == null)
		{
			return;
		}

		var basePosition = visualRoot.Position;
		visualRoot.Scale = Vector3.One * 0.82f;
		visualRoot.Position = basePosition + new Vector3(0.0f, -0.18f, 0.0f);

		var tween = buildingRoot.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(visualRoot, "scale", Vector3.One, durationSeconds)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visualRoot, "position", basePosition, durationSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}
}
