using Godot;

namespace Vilanova;

public partial class BuildControlsHint : CanvasLayer
{
	[Export] public NodePath BuildingManagerPath { get; set; } = new("../BuildingManager");

	private BuildingManager _buildingManager = null!;
	private Label _hintLabel = null!;
	private Label _statusLabel = null!;

	public override void _Ready()
	{
		_buildingManager = GetNode<BuildingManager>(BuildingManagerPath);
		_hintLabel = GetNode<Label>("MarginContainer/VBox/PanelContainer/HintLabel");
		_statusLabel = GetNode<Label>("MarginContainer/VBox/StatusLabel");

		var margin = GetNode<MarginContainer>("MarginContainer");
		margin.MouseFilter = Control.MouseFilterEnum.Ignore;
		GetNode<VBoxContainer>("MarginContainer/VBox").MouseFilter = Control.MouseFilterEnum.Ignore;

		var panel = GetNode<PanelContainer>("MarginContainer/VBox/PanelContainer");
		panel.MouseFilter = Control.MouseFilterEnum.Stop;
		panel.AddThemeStyleboxOverride("panel", CozyUiTheme.Chip());
		_hintLabel.AddThemeColorOverride("font_color", CozyUiTheme.Ink);
		_hintLabel.AddThemeFontSizeOverride("font_size", CozyUiTheme.SmallSize);
		_hintLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		_statusLabel.AddThemeColorOverride("font_color", CozyUiTheme.Terracotta);
		_statusLabel.AddThemeFontSizeOverride("font_size", CozyUiTheme.SmallSize);
		_statusLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
	}

	public override void _Process(double delta)
	{
		_hintLabel.Text = _buildingManager.GetModeHintText();
		var feedback = _buildingManager.PlacementFeedbackText;
		_statusLabel.Text = feedback;
		_statusLabel.Visible = !string.IsNullOrEmpty(feedback);
	}
}
