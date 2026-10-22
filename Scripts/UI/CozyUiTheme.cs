using Godot;

namespace Vilanova;

/// <summary>
/// Shared colors, spacing, and style boxes for the cozy village interface.
/// </summary>
public static class CozyUiTheme
{
	public static readonly Color Parchment = new("f4e6cf");
	public static readonly Color ParchmentDeep = new("e7d3b4");
	public static readonly Color CardFill = new("fff8ee");
	public static readonly Color CardHover = new("fffdf8");
	public static readonly Color Ink = new("3c291c");
	public static readonly Color InkSoft = new("6d5344");
	public static readonly Color Wood = new("8a5a34");
	public static readonly Color WoodDark = new("5c3b24");
	public static readonly Color Terracotta = new("c06840");
	public static readonly Color Moss = new("4f9160");
	public static readonly Color Wall = new("f3e8d4");

	public const int PanelRadius = 12;
	public const int CardRadius = 8;
	public const int TitleSize = 22;
	public const int BodySize = 15;
	public const int TabSize = 14;
	public const int CardNameSize = 13;
	public const int SmallSize = 12;
	public const int Margin = 14;
	public const int Gap = 8;

	public static StyleBoxFlat Panel()
	{
		return new StyleBoxFlat
		{
			BgColor = Parchment,
			BorderColor = Wood,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = PanelRadius,
			CornerRadiusTopRight = PanelRadius,
			CornerRadiusBottomRight = PanelRadius,
			CornerRadiusBottomLeft = PanelRadius,
			ContentMarginLeft = Margin,
			ContentMarginTop = Margin,
			ContentMarginRight = Margin,
			ContentMarginBottom = Margin,
			ShadowColor = new Color(0.28f, 0.16f, 0.08f, 0.22f),
			ShadowSize = 10,
			ShadowOffset = new Vector2(0, 4),
		};
	}

	public static StyleBoxFlat Card(bool hovered, bool selected)
	{
		var box = new StyleBoxFlat
		{
			BgColor = hovered ? CardHover : CardFill,
			BorderColor = selected ? Terracotta : new Color(Wood, 0.45f),
			CornerRadiusTopLeft = CardRadius,
			CornerRadiusTopRight = CardRadius,
			CornerRadiusBottomRight = CardRadius,
			CornerRadiusBottomLeft = CardRadius,
			ContentMarginLeft = 8,
			ContentMarginTop = 8,
			ContentMarginRight = 8,
			ContentMarginBottom = 8,
			ShadowColor = new Color(0.28f, 0.16f, 0.08f, hovered || selected ? 0.2f : 0.08f),
			ShadowSize = hovered ? 6 : 2,
			ShadowOffset = new Vector2(0, hovered ? 2 : 1),
		};
		var width = selected ? 3 : 1;
		box.BorderWidthLeft = width;
		box.BorderWidthTop = width;
		box.BorderWidthRight = width;
		box.BorderWidthBottom = width;
		return box;
	}

	public static StyleBoxFlat Tab(bool selected)
	{
		var box = new StyleBoxFlat
		{
			BgColor = selected ? Terracotta : ParchmentDeep,
			BorderColor = selected ? Terracotta : new Color(Wood, 0.35f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ContentMarginLeft = 6,
			ContentMarginTop = 6,
			ContentMarginRight = 6,
			ContentMarginBottom = 6,
		};
		return box;
	}

	public static StyleBoxFlat CloseButton(bool hovered)
	{
		return new StyleBoxFlat
		{
			BgColor = hovered ? Terracotta : ParchmentDeep,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ContentMarginLeft = 4,
			ContentMarginTop = 2,
			ContentMarginRight = 4,
			ContentMarginBottom = 2,
		};
	}

	public static StyleBoxFlat Chip()
	{
		return new StyleBoxFlat
		{
			BgColor = ParchmentDeep,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6,
			ContentMarginLeft = 8,
			ContentMarginTop = 4,
			ContentMarginRight = 8,
			ContentMarginBottom = 4,
		};
	}

	public static StyleBoxFlat Details()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(ParchmentDeep, 0.65f),
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ContentMarginLeft = 10,
			ContentMarginTop = 8,
			ContentMarginRight = 10,
			ContentMarginBottom = 8,
		};
	}

	public static StyleBoxFlat ThumbnailWell()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color("efe2cc"),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6,
		};
	}
}
