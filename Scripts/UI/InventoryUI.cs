using Godot;

namespace Vilanova;

public partial class InventoryUI : CanvasLayer
{
	public const int Columns = 5;

	[Export] public NodePath InventoryPath { get; set; } = new("../Player/Inventory");
	[Export] public NodePath BuildingManagerPath { get; set; } = new("../BuildingManager");
	[Export] public NodePath InfoPanelPath { get; set; } = new("../BuildingInfoPanelUI");

	private InventoryComponent _inventory = null!;
	private BuildingManager _buildings = null!;
	private BuildingInfoPanelUI _info = null!;
	private Control _root = null!;
	private PanelContainer _panel = null!;
	private ScrollContainer _scroll = null!;
	private GridContainer _grid = null!;
	private InventorySlotView[] _slots = System.Array.Empty<InventorySlotView>();
	private int _selected;
	private Vector2 _laidOutFor;
	private bool _open;

	public bool IsOpen => _open;

	public override void _Ready()
	{
		Layer = 6;
		_inventory = GetNode<InventoryComponent>(InventoryPath);
		_buildings = GetNode<BuildingManager>(BuildingManagerPath);
		_info = GetNode<BuildingInfoPanelUI>(InfoPanelPath);

		_root = new Control
		{
			Name = "Root",
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		_panel = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		_panel.AddThemeStyleboxOverride("panel", CozyUiTheme.Panel());
		_root.AddChild(_panel);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", CozyUiTheme.Gap);
		_panel.AddChild(box);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", CozyUiTheme.Gap);
		box.AddChild(header);

		var title = new Label { Text = "Mochila", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		title.AddThemeColorOverride("font_color", CozyUiTheme.Ink);
		title.AddThemeFontSizeOverride("font_size", CozyUiTheme.TitleSize);
		header.AddChild(title);

		var close = new Button
		{
			Text = "×",
			FocusMode = Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(36, 32),
			TooltipText = "Fechar (I)",
		};
		close.AddThemeStyleboxOverride("normal", CozyUiTheme.CloseButton(false));
		close.AddThemeStyleboxOverride("hover", CozyUiTheme.CloseButton(true));
		close.AddThemeStyleboxOverride("pressed", CozyUiTheme.CloseButton(true));
		close.AddThemeColorOverride("font_color", CozyUiTheme.Ink);
		close.Pressed += () => SetOpen(false);
		header.AddChild(close);

		_scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		box.AddChild(_scroll);

		_grid = new GridContainer
		{
			Columns = Columns,
			MouseFilter = Control.MouseFilterEnum.Pass,
		};
		_grid.AddThemeConstantOverride("h_separation", CozyUiTheme.Gap);
		_grid.AddThemeConstantOverride("v_separation", CozyUiTheme.Gap);
		_scroll.AddChild(_grid);

		var hint = new Label { Text = "I abre e fecha    Esc fecha" };
		hint.AddThemeColorOverride("font_color", CozyUiTheme.InkSoft);
		hint.AddThemeFontSizeOverride("font_size", CozyUiTheme.SmallSize);
		box.AddChild(hint);

		BuildSlots();
		_inventory.InventoryChanged += Refresh;
		_root.Resized += ApplyLayout;
		SetOpen(false);
		CallDeferred(nameof(ApplyLayout));
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsEcho())
		{
			return;
		}

		if (@event.IsActionPressed("inventory_toggle"))
		{
			SetOpen(!_open);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!_open || !@event.IsActionPressed("build_cancel"))
		{
			return;
		}

		if (_info != null && _info.IsOpen)
		{
			return;
		}

		if (_buildings != null && _buildings.Mode != BuildingMode.Idle)
		{
			return;
		}

		SetOpen(false);
		GetViewport().SetInputAsHandled();
	}

	public void SetOpen(bool open)
	{
		_open = open;
		_root.Visible = open;
		if (open)
		{
			ApplyLayout();
			Refresh();
		}
	}

	private void BuildSlots()
	{
		var count = _inventory.Capacity;
		_slots = new InventorySlotView[count];
		for (var i = 0; i < count; i++)
		{
			var index = i;
			var slot = new InventorySlotView();
			slot.SlotPressed += () =>
			{
				_selected = index;
				Refresh();
			};
			_grid.AddChild(slot);
			_slots[i] = slot;
		}
	}

	private void ApplyLayout()
	{
		if (_root == null || _grid == null)
		{
			return;
		}

		var size = _root.Size;
		if (size.X < 8f || size.Y < 8f || size == _laidOutFor)
		{
			return;
		}

		_laidOutFor = size;
		var gaps = CozyUiTheme.Gap * (Columns - 1);
		var slot = Mathf.Clamp((size.X * 0.42f - gaps) / Columns, 48f, 68f);
		var gridWidth = slot * Columns + gaps;
		var rows = Mathf.CeilToInt(_slots.Length / (float)Columns);
		var gridHeight = slot * rows + CozyUiTheme.Gap * (rows - 1);
		var chrome = 96f;
		var available = Mathf.Max(160f, size.Y - 48f);
		if (gridHeight + chrome > available)
		{
			slot = Mathf.Clamp((available - chrome - CozyUiTheme.Gap * (rows - 1)) / rows, 44f, slot);
			gridWidth = slot * Columns + gaps;
			gridHeight = slot * rows + CozyUiTheme.Gap * (rows - 1);
		}

		foreach (var view in _slots)
		{
			view.SetSlotSize(slot);
		}

		var gridSize = new Vector2(gridWidth, gridHeight);
		if (_grid.CustomMinimumSize != gridSize)
		{
			_grid.CustomMinimumSize = gridSize;
		}

		var scrollHeight = Mathf.Min(gridHeight, Mathf.Max(120f, available - chrome));
		_scroll.CustomMinimumSize = new Vector2(gridWidth, scrollHeight);

		var panelWidth = gridWidth + CozyUiTheme.Margin * 2f + 8f;
		var panelHeight = scrollHeight + chrome;
		_panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		_panel.OffsetLeft = -panelWidth * 0.5f;
		_panel.OffsetRight = panelWidth * 0.5f;
		_panel.OffsetTop = -panelHeight * 0.5f;
		_panel.OffsetBottom = panelHeight * 0.5f;
	}

	private void Refresh()
	{
		if (_slots == null)
		{
			return;
		}

		for (var i = 0; i < _slots.Length; i++)
		{
			var slot = _inventory.GetSlot(i);
			var definition = slot == null || slot.IsEmpty ? null : _inventory.FindDefinition(slot.ItemId);
			_slots[i].Bind(definition, slot?.Quantity ?? 0, i == _selected);
		}
	}
}
