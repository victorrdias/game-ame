using Godot;

namespace Vilanova;

public partial class BuildingInfoPanelUI : CanvasLayer
{
	private PanelContainer _panel = null!;
	private Label _bodyLabel = null!;
	private bool _isOpen;

	public bool IsOpen => _isOpen;

	public override void _Ready()
	{
		Layer = 4;

		var root = new Control
		{
			Name = "Root",
			AnchorsPreset = (int)Control.LayoutPreset.FullRect,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		AddChild(root);

		_panel = new PanelContainer
		{
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		_panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_panel.OffsetLeft = -180;
		_panel.OffsetTop = 108;
		_panel.OffsetRight = 180;
		_panel.OffsetBottom = 248;
		root.AddChild(_panel);

		_panel.AddThemeStyleboxOverride("panel", CozyUiTheme.Panel());

		_bodyLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_bodyLabel.AddThemeColorOverride("font_color", CozyUiTheme.Ink);
		_bodyLabel.AddThemeFontSizeOverride("font_size", CozyUiTheme.BodySize);
		_panel.AddChild(_bodyLabel);
	}

	public override void _Input(InputEvent @event)
	{
		if (!_isOpen || !@event.IsActionPressed("build_cancel"))
		{
			return;
		}

		Close();
		GetViewport().SetInputAsHandled();
	}

	public void ShowBuilding(PlacedBuilding building, Vector3 worldCenter)
	{
		if (building?.Definition == null)
		{
			return;
		}

		var size = FootprintHelper.GetRotatedSize(building.Definition.SizeInCells, building.RotationSteps);
		_bodyLabel.Text =
			$"{building.Definition.DisplayName}\n" +
			$"Categoria: {BuildingCategoryLabels.ToDisplayName(building.Definition.Category)}\n" +
			$"Dimensões: {size.X}×{size.Y} subtiles\n" +
			$"Posição: ({worldCenter.X:0.#}, {worldCenter.Z:0.#})";

		_panel.Visible = true;
		_isOpen = true;
	}

	public void Close()
	{
		_panel.Visible = false;
		_isOpen = false;
	}
}
