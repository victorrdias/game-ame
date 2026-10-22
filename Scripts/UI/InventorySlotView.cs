using Godot;

namespace Vilanova;

public partial class InventorySlotView : PanelContainer
{
	[Signal]
	public delegate void SlotPressedEventHandler();

	private TextureRect _icon = null!;
	private Label _count = null!;
	private bool _selected;
	private bool _hovered;

	public override void _Ready()
	{
		MouseFilter = Control.MouseFilterEnum.Stop;
		ClipContents = true;
		MouseEntered += () =>
		{
			_hovered = true;
			ApplyStyle();
		};
		MouseExited += () =>
		{
			_hovered = false;
			ApplyStyle();
		};

		var face = new Control
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		face.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(face);

		_icon = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_icon.OffsetLeft = 8;
		_icon.OffsetTop = 6;
		_icon.OffsetRight = -8;
		_icon.OffsetBottom = -14;
		face.AddChild(_icon);

		_count = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_count.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_count.OffsetLeft = 4;
		_count.OffsetRight = -4;
		_count.OffsetBottom = -2;
		_count.AddThemeColorOverride("font_color", CozyUiTheme.Ink);
		_count.AddThemeFontSizeOverride("font_size", CozyUiTheme.SmallSize);
		face.AddChild(_count);
		ApplyStyle();
	}

	public void SetSlotSize(float size)
	{
		CustomMinimumSize = new Vector2(size, size);
	}

	public void Bind(ItemDefinition definition, int quantity, bool selected)
	{
		_selected = selected;
		if (_icon == null || _count == null)
		{
			return;
		}

		if (quantity <= 0)
		{
			_icon.Texture = null;
			_count.Text = string.Empty;
			TooltipText = string.Empty;
			ApplyStyle();
			return;
		}

		_icon.Texture = ItemIconPainter.Get(definition);
		_count.Text = quantity.ToString();
		if (definition == null)
		{
			TooltipText = string.Empty;
			ApplyStyle();
			return;
		}

		var name = string.IsNullOrEmpty(definition.DisplayName) ? definition.Id : definition.DisplayName;
		var description = definition.Description ?? string.Empty;
		TooltipText = string.IsNullOrEmpty(description) ? name : name + "\n" + description;
		ApplyStyle();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
		{
			EmitSignal(SignalName.SlotPressed);
			AcceptEvent();
		}
	}

	private void ApplyStyle()
	{
		AddThemeStyleboxOverride("panel", CozyUiTheme.Card(_hovered, _selected));
	}
}
