using Godot;

namespace Vilanova;

public partial class BuildDetailsPanel : PanelContainer
{
	private Label _nameLabel = null!;
	private Label _descriptionLabel = null!;
	private Label _footprintLabel = null!;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		AddThemeStyleboxOverride("panel", CozyUiTheme.Details());

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 4);
		box.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(box);

		_nameLabel = MakeLabel(CozyUiTheme.BodySize, CozyUiTheme.Ink);
		_descriptionLabel = MakeLabel(CozyUiTheme.SmallSize, CozyUiTheme.InkSoft);
		_footprintLabel = MakeLabel(CozyUiTheme.SmallSize, CozyUiTheme.WoodDark);
		box.AddChild(_nameLabel);
		box.AddChild(_descriptionLabel);
		box.AddChild(_footprintLabel);
		ShowPrompt();
	}

	public void ShowDefinition(BuildingDefinition definition)
	{
		if (definition == null)
		{
			ShowPrompt();
			return;
		}

		_nameLabel.Text = definition.DisplayName;
		_descriptionLabel.Text = string.IsNullOrWhiteSpace(definition.Description)
			? $"{BuildingCategoryLabels.ToDisplayName(definition.Category)} para colocar na vila."
			: definition.Description;
		_footprintLabel.Text = $"Ocupa {definition.SizeInCells.X}×{definition.SizeInCells.Y} subtiles";
	}

	public void ShowPrompt()
	{
		_nameLabel.Text = "Escolha uma peça";
		_descriptionLabel.Text = "Selecione um card para ver o tamanho no chão e começar a construir.";
		_footprintLabel.Text = string.Empty;
	}

	private static Label MakeLabel(int size, Color color)
	{
		var label = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}
}
