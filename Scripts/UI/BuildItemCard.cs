using Godot;

namespace Vilanova;

public partial class BuildItemCard : PanelContainer
{
	[Signal]
	public delegate void PressedEventHandler();

	private Label _nameLabel = null!;
	private Label _stateLabel = null!;
	private bool _selected;
	private bool _hovered;
	private bool _available = true;

	public BuildingDefinition Definition { get; private set; }

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		FocusMode = FocusModeEnum.None;
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		ClipContents = true;
		CustomMinimumSize = new Vector2(0, 132);
		MouseEntered += OnHover;
		MouseExited += OnLeave;
	}

	public void Bind(BuildingDefinition definition, bool available)
	{
		Definition = definition;
		_available = available;
		TooltipText = definition.DisplayName;

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		box.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(box);

		var well = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0, 72),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		well.AddThemeStyleboxOverride("panel", CozyUiTheme.ThumbnailWell());
		box.AddChild(well);

		var image = new TextureRect
		{
			Texture = BuildThumbnailLibrary.Get(definition),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(0, 72),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		well.AddChild(image);

		_nameLabel = new Label
		{
			Text = definition.DisplayName,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			MaxLinesVisible = 2,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 34),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_nameLabel.AddThemeFontSizeOverride("font_size", CozyUiTheme.CardNameSize);
		_nameLabel.AddThemeColorOverride("font_color", CozyUiTheme.Ink);
		box.AddChild(_nameLabel);

		_stateLabel = new Label
		{
			Text = "Indisponível",
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = !available,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_stateLabel.AddThemeFontSizeOverride("font_size", CozyUiTheme.SmallSize);
		_stateLabel.AddThemeColorOverride("font_color", CozyUiTheme.InkSoft);
		box.AddChild(_stateLabel);

		ApplyVisual();
	}

	public void SetSelected(bool selected)
	{
		_selected = selected;
		ApplyVisual();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		if (_available)
		{
			EmitSignal(SignalName.Pressed);
		}

		AcceptEvent();
	}

	private void OnHover()
	{
		_hovered = true;
		ApplyVisual();
	}

	private void OnLeave()
	{
		_hovered = false;
		ApplyVisual();
	}

	private void ApplyVisual()
	{
		AddThemeStyleboxOverride("panel", CozyUiTheme.Card(_hovered && _available, _selected && _available));
		Modulate = _available ? Colors.White : new Color(1, 1, 1, 0.55f);
		if (_nameLabel != null)
		{
			_nameLabel.Modulate = _available ? Colors.White : new Color(1, 1, 1, 0.8f);
		}
	}
}
