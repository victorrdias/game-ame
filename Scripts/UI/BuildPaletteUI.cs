using Godot;

namespace Vilanova;

public partial class BuildPaletteUI : CanvasLayer
{
	[Export] public NodePath BuildingManagerPath { get; set; } = new("../BuildingManager");

	private BuildingManager _buildingManager = null!;
	private Control _root = null!;
	private BuildingCategory _activeCategory = BuildingCategory.Buildings;
	private Label _detailsLabel = null!;
	private GridContainer _itemGrid = null!;
	private readonly System.Collections.Generic.List<Button> _categoryButtons = new();
	private bool _isOpen;

	public bool IsOpen => _isOpen;

	public override void _Ready()
	{
		_buildingManager = GetNode<BuildingManager>(BuildingManagerPath);
		_root = GetNode<Control>("RootPanel");
		BuildUiChrome();
		SelectCategory(BuildingCategory.Buildings);
		SetOpen(false);
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
		_isOpen = open;
		_root.Visible = open;
	}

	private void BuildUiChrome()
	{
		var root = GetNode<Control>("RootPanel");
		root.MouseFilter = Control.MouseFilterEnum.Stop;

		_detailsLabel = root.GetNode<Label>("PanelFrame/VBox/DetailsLabel");
		_itemGrid = root.GetNode<GridContainer>("PanelFrame/VBox/ItemScroll/ItemGrid");

		var categories = root.GetNode<HBoxContainer>("PanelFrame/VBox/CategoryRow");
		AddCategoryButton(categories, "Edifícios", BuildingCategory.Buildings);
		AddCategoryButton(categories, "Natureza", BuildingCategory.Nature);
		AddCategoryButton(categories, "Decoração", BuildingCategory.Decoration);
		AddCategoryButton(categories, "Caminhos", BuildingCategory.Paths);

		var panel = root.GetNode<PanelContainer>("PanelFrame");
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.11f, 0.13f, 0.1f, 0.82f),
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
		};
		panel.AddThemeStyleboxOverride("panel", style);
	}

	private void AddCategoryButton(HBoxContainer row, string text, BuildingCategory category)
	{
		var button = new Button { Text = text, ToggleMode = true };
		button.Pressed += () => SelectCategory(category);
		row.AddChild(button);
		_categoryButtons.Add(button);
	}

	private void SelectCategory(BuildingCategory category)
	{
		_activeCategory = category;
		for (var i = 0; i < _categoryButtons.Count; i++)
		{
			_categoryButtons[i].ButtonPressed = i == (int)category;
		}

		RebuildItemGrid();
		_detailsLabel.Text = "Selecione um item do catálogo.";
	}

	private void RebuildItemGrid()
	{
		foreach (var child in _itemGrid.GetChildren())
		{
			child.QueueFree();
		}

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

			var itemButton = new Button
			{
				Text = definition.DisplayName,
				CustomMinimumSize = new Vector2(130, 36),
			};
			itemButton.Pressed += () => OnItemSelected(definition);
			_itemGrid.AddChild(itemButton);
		}
	}

	private void OnItemSelected(BuildingDefinition definition)
	{
		_detailsLabel.Text =
			$"{definition.DisplayName}\nFootprint: {definition.SizeInCells.X}x{definition.SizeInCells.Y} células";
		_buildingManager.BeginPlacing(definition);
	}
}
