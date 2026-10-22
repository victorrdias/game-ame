using Godot;
using Godot.Collections;

namespace Vilanova;

public partial class BuildPaletteUI : CanvasLayer
{
	[Export] public NodePath BuildingManagerPath { get; set; } = new("../BuildingManager");

	private const float PanelMargin = 16f;
	private const float Slide = 48f;

	private BuildingManager _buildingManager = null!;
	private Control _root = null!;
	private PanelContainer _panel = null!;
	private GridContainer _itemGrid = null!;
	private ScrollContainer _scroll = null!;
	private BuildDetailsPanel _details = null!;
	private readonly Array<BuildCategoryTab> _tabs = new();
	private GridContainer _tabGrid = null!;
	private readonly Array<BuildItemCard> _cards = new();
	private BuildingCategory _activeCategory = BuildingCategory.Buildings;
	private BuildingDefinition _selected;
	private Tween _motion;
	private bool _isOpen;
	private bool _categoryDirty;
	private float _lastPanelWidth = -1f;
	private float _pinnedForWidth = -1f;
	private bool _applyingLayout;

	public bool IsOpen => _isOpen;

	public override void _Ready()
	{
		_buildingManager = GetNode<BuildingManager>(BuildingManagerPath);
		_root = GetNode<Control>("RootPanel");
		_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Ignore;
		BuildChrome();
		SelectCategory(BuildingCategory.Buildings);
		_isOpen = false;
		_root.Visible = false;
		ParkPanel();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("build_menu"))
		{
			return;
		}

		SetOpen(!_isOpen);
		GetViewport().SetInputAsHandled();
	}

	public void SetOpen(bool open)
	{
		if (_panel == null)
		{
			return;
		}

		_isOpen = open;
		GetNodeOrNull<TerrainGrid>("../TerrainGrid")?.SetVisibleForConstruction(open);
		_motion?.Kill();

		if (open)
		{
			_root.Visible = true;
			var left = RestLeft();
			_panel.Modulate = new Color(1, 1, 1, 0);
			_panel.OffsetRight = -PanelMargin + Slide;
			_panel.OffsetLeft = left + Slide;
			_motion = CreateTween();
			_motion.SetParallel(true);
			_motion.TweenProperty(_panel, "modulate:a", 1.0f, 0.18f);
			_motion.TweenProperty(_panel, "offset_left", left, 0.18f);
			_motion.TweenProperty(_panel, "offset_right", -PanelMargin, 0.18f);
			return;
		}

		if (!_root.Visible)
		{
			ParkPanel();
			return;
		}

		_motion = CreateTween();
		_motion.SetParallel(true);
		_motion.TweenProperty(_panel, "modulate:a", 0.0f, 0.14f);
		_motion.TweenProperty(_panel, "offset_left", RestLeft() + Slide, 0.14f);
		_motion.TweenProperty(_panel, "offset_right", -PanelMargin + Slide, 0.14f);
		_motion.Chain().TweenCallback(Callable.From(HideIfClosed));
	}

	private void HideIfClosed()
	{
		if (!_isOpen)
		{
			_root.Visible = false;
		}
	}

	private float RestLeft()
	{
		var viewportWidth = GetViewport().GetVisibleRect().Size.X;
		var width = Mathf.Clamp(viewportWidth * 0.28f, 300f, 540f);
		return -PanelMargin - width;
	}

	private void PinPanel()
	{
		var nested = _applyingLayout;
		_applyingLayout = true;
		_pinnedForWidth = GetViewport().GetVisibleRect().Size.X;
		_panel.AnchorLeft = 1f;
		_panel.AnchorRight = 1f;
		_panel.AnchorTop = 0f;
		_panel.AnchorBottom = 1f;
		_panel.OffsetTop = PanelMargin;
		_panel.OffsetBottom = -PanelMargin;
		_panel.OffsetRight = -PanelMargin;
		_panel.OffsetLeft = RestLeft();
		if (!nested)
		{
			_applyingLayout = false;
		}
	}

	private void ParkPanel()
	{
		_applyingLayout = true;
		PinPanel();
		_panel.Modulate = new Color(1, 1, 1, 0);
		_panel.OffsetLeft += Slide;
		_panel.OffsetRight += Slide;
		_applyingLayout = false;
	}

	private void BuildChrome()
	{
		_panel = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		_panel.AddThemeStyleboxOverride("panel", CozyUiTheme.Panel());
		PinPanel();
		_panel.Resized += OnPanelResized;
		_root.AddChild(_panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", CozyUiTheme.Gap);
		column.MouseFilter = Control.MouseFilterEnum.Ignore;
		_panel.AddChild(column);

		column.AddChild(BuildHeader());
		column.AddChild(BuildTabs());

		_scroll = new ScrollContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		column.AddChild(_scroll);

		_itemGrid = new GridContainer
		{
			Columns = 2,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_itemGrid.AddThemeConstantOverride("h_separation", CozyUiTheme.Gap);
		_itemGrid.AddThemeConstantOverride("v_separation", CozyUiTheme.Gap);
		_scroll.AddChild(_itemGrid);

		_details = new BuildDetailsPanel();
		column.AddChild(_details);
		column.AddChild(BuildFooter());
		Callable.From(OnPanelResized).CallDeferred();
	}

	private Control BuildHeader()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.MouseFilter = Control.MouseFilterEnum.Ignore;
		row.AddChild(new BuildMenuMark());

		var title = new Label
		{
			Text = "Construção",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		title.AddThemeFontSizeOverride("font_size", CozyUiTheme.TitleSize);
		title.AddThemeColorOverride("font_color", CozyUiTheme.Ink);
		row.AddChild(title);

		var close = new Button
		{
			Text = "×",
			FocusMode = Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(32, 32),
			TooltipText = "Fechar (U)",
		};
		StyleClose(close, false);
		close.MouseEntered += () => StyleClose(close, true);
		close.MouseExited += () => StyleClose(close, false);
		close.Pressed += () => SetOpen(false);
		row.AddChild(close);
		return row;
	}

	private static void StyleClose(Button button, bool hovered)
	{
		var font = hovered ? CozyUiTheme.CardFill : CozyUiTheme.Ink;
		button.AddThemeFontSizeOverride("font_size", 20);
		button.AddThemeColorOverride("font_color", font);
		button.AddThemeColorOverride("font_hover_color", font);
		button.AddThemeColorOverride("font_pressed_color", font);
		var style = CozyUiTheme.CloseButton(hovered);
		button.AddThemeStyleboxOverride("normal", style);
		button.AddThemeStyleboxOverride("hover", CozyUiTheme.CloseButton(true));
		button.AddThemeStyleboxOverride("pressed", style);
		button.AddThemeStyleboxOverride("focus", style);
	}

	private Control BuildTabs()
	{
		_tabGrid = new GridContainer
		{
			Columns = 2,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_tabGrid.AddThemeConstantOverride("h_separation", 6);
		_tabGrid.AddThemeConstantOverride("v_separation", 6);
		AddTab(_tabGrid, BuildingCategory.Buildings);
		AddTab(_tabGrid, BuildingCategory.Nature);
		AddTab(_tabGrid, BuildingCategory.Decoration);
		AddTab(_tabGrid, BuildingCategory.Paths);
		return _tabGrid;
	}

	private void AddTab(GridContainer row, BuildingCategory category)
	{
		var tab = new BuildCategoryTab();
		tab.Setup(BuildingCategoryLabels.ToDisplayName(category), category);
		tab.Pressed += () => SelectCategory(category);
		row.AddChild(tab);
		_tabs.Add(tab);
	}

	private Control BuildFooter()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		row.MouseFilter = Control.MouseFilterEnum.Ignore;
		row.AddChild(MakeChip("R  girar"));
		row.AddChild(MakeChip("X  demolir"));
		row.AddChild(MakeChip("Esc  cancelar"));
		return row;
	}

	private static PanelContainer MakeChip(string text)
	{
		var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		chip.AddThemeStyleboxOverride("panel", CozyUiTheme.Chip());
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", CozyUiTheme.SmallSize);
		label.AddThemeColorOverride("font_color", CozyUiTheme.InkSoft);
		chip.AddChild(label);
		return chip;
	}

	private void SelectCategory(BuildingCategory category)
	{
		_activeCategory = category;
		if (_categoryDirty)
		{
			return;
		}

		_categoryDirty = true;
		Callable.From(ApplyCategorySelection).CallDeferred();
	}

	private void ApplyCategorySelection()
	{
		_categoryDirty = false;
		foreach (var tab in _tabs)
		{
			tab.SetSelected(tab.Category == _activeCategory);
		}

		RebuildItemGrid();
	}

	private void RebuildItemGrid()
	{
		foreach (var child in _itemGrid.GetChildren())
		{
			_itemGrid.RemoveChild(child);
			child.QueueFree();
		}

		_cards.Clear();
		var catalog = _buildingManager.Catalog;
		if (catalog?.Items == null)
		{
			return;
		}

		foreach (var definition in catalog.Items)
		{
			if (definition == null || definition.Category != _activeCategory)
			{
				continue;
			}

			var card = new BuildItemCard();
			var item = definition;
			card.Pressed += () => OnItemSelected(item);
			_itemGrid.AddChild(card);
			card.Bind(item, true);
			card.SetSelected(_selected != null && _selected.Id == item.Id);
			_cards.Add(card);
		}

		if (_selected != null && _selected.Category == _activeCategory)
		{
			_details.ShowDefinition(_selected);
		}
		else if (_selected == null)
		{
			_details.ShowPrompt();
		}
	}

	private void OnItemSelected(BuildingDefinition definition)
	{
		_selected = definition;
		foreach (var card in _cards)
		{
			card.SetSelected(card.Definition == definition);
		}

		_details.ShowDefinition(definition);
		_buildingManager.BeginPlacing(definition);
	}

	private int _resizeDepth;

	private void OnPanelResized()
	{
		if (_resizeDepth > 0 || _applyingLayout || _panel == null || _scroll == null || _itemGrid == null)
		{
			return;
		}

		_resizeDepth++;

		var viewportWidth = GetViewport().GetVisibleRect().Size.X;
		if (viewportWidth > 2f && !Mathf.IsEqualApprox(viewportWidth, _pinnedForWidth))
		{
			if (_isOpen)
			{
				PinPanel();
				_panel.Modulate = Colors.White;
			}
			else
			{
				ParkPanel();
			}
		}

		var width = _panel.Size.X;
		if (width < 2f || Mathf.IsEqualApprox(width, _lastPanelWidth))
		{
			_resizeDepth--;
			return;
		}

		_lastPanelWidth = width;
		var columns = width >= 400f ? 3 : 2;
		if (_itemGrid.Columns != columns)
		{
			_itemGrid.Columns = columns;
		}

		var tabColumns = width >= 560f ? 4 : 2;
		if (_tabGrid != null && _tabGrid.Columns != tabColumns)
		{
			_tabGrid.Columns = tabColumns;
		}

		var bar = _scroll.GetVScrollBar();
		var barWidth = bar.Size.X;
		if (barWidth < 8f)
		{
			barWidth = 14f;
		}

		var contentWidth = Mathf.Max(0f, _scroll.Size.X - barWidth);
		if (contentWidth > 1f && !Mathf.IsEqualApprox(_itemGrid.CustomMinimumSize.X, contentWidth))
		{
			_itemGrid.CustomMinimumSize = new Vector2(contentWidth, 0);
		}

		_resizeDepth--;
	}
}
