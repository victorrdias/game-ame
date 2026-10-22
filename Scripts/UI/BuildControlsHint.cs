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

		var panel = GetNode<PanelContainer>("MarginContainer/VBox/PanelContainer");
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.14f, 0.11f, 0.45f),
			ContentMarginLeft = 10,
			ContentMarginTop = 6,
			ContentMarginRight = 10,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
		};
		panel.AddThemeStyleboxOverride("panel", panelStyle);
	}

	public override void _Process(double delta)
	{
		_hintLabel.Text = _buildingManager.GetModeHintText();
		var feedback = _buildingManager.PlacementFeedbackText;
		_statusLabel.Text = feedback;
		_statusLabel.Visible = !string.IsNullOrEmpty(feedback);
	}
}
