using Godot;

namespace Vilanova;

/// <summary>
/// Small cottage mark for the build menu header.
/// </summary>
public partial class BuildMenuMark : Control
{
	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(36, 36);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(8, 16, 20, 14), CozyUiTheme.Wall);
		DrawColoredPolygon(
			new[] { new Vector2(6, 17), new Vector2(18, 6), new Vector2(30, 17) },
			CozyUiTheme.Terracotta);
		DrawRect(new Rect2(15, 20, 6, 10), CozyUiTheme.WoodDark);
	}
}
