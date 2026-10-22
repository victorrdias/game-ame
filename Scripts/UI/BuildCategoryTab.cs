using Godot;

namespace Vilanova;

public partial class BuildCategoryTab : Button
{
	public BuildingCategory Category { get; private set; }

	public void Setup(string text, BuildingCategory category)
	{
		Category = category;
		Text = text;
		TooltipText = text;
		ToggleMode = false;
		FocusMode = FocusModeEnum.None;
		ClipText = true;
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		CustomMinimumSize = new Vector2(0, 32);
		AddThemeFontSizeOverride("font_size", CozyUiTheme.TabSize);
		SetSelected(false);
	}

	public void SetSelected(bool selected)
	{
		var font = selected ? CozyUiTheme.CardFill : CozyUiTheme.Ink;
		AddThemeColorOverride("font_color", font);
		AddThemeColorOverride("font_hover_color", font);
		AddThemeColorOverride("font_pressed_color", font);
		AddThemeColorOverride("font_focus_color", font);
		var style = CozyUiTheme.Tab(selected);
		AddThemeStyleboxOverride("normal", style);
		AddThemeStyleboxOverride("hover", CozyUiTheme.Tab(true));
		AddThemeStyleboxOverride("pressed", style);
		AddThemeStyleboxOverride("focus", style);
	}
}
