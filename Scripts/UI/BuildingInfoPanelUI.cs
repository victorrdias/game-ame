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

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.1f, 0.12f, 0.09f, 0.88f),
			ContentMarginLeft = 14,
			ContentMarginTop = 12,
			ContentMarginRight = 14,
			ContentMarginBottom = 12,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
		};
		_panel.AddThemeStyleboxOverride("panel", style);

		_bodyLabel = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
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
