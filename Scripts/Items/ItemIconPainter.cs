using Godot;
using System.Collections.Generic;

namespace Vilanova;

public static class ItemIconPainter
{
	private static readonly Dictionary<string, Texture2D> Cache = new();

	public static Texture2D Get(ItemDefinition definition)
	{
		if (definition == null)
		{
			return null;
		}

		if (definition.Icon != null)
		{
			return definition.Icon;
		}

		if (string.IsNullOrEmpty(definition.Id))
		{
			return null;
		}

		if (Cache.TryGetValue(definition.Id, out var cached))
		{
			return cached;
		}

		var painted = definition.Id == ItemIds.Stone ? Stone() : Wood();
		Cache[definition.Id] = painted;
		return painted;
	}

	private static Texture2D Wood()
	{
		var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
		image.Fill(new Color(0, 0, 0, 0));
		image.FillRect(new Rect2I(6, 13, 20, 8), new Color("9c6b43"));
		image.FillRect(new Rect2I(6, 13, 4, 8), new Color("6f4a2e"));
		image.FillRect(new Rect2I(22, 13, 4, 8), new Color("6f4a2e"));
		image.FillRect(new Rect2I(8, 15, 16, 2), new Color("c4a574"));
		return ImageTexture.CreateFromImage(image);
	}

	private static Texture2D Stone()
	{
		var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
		image.Fill(new Color(0, 0, 0, 0));
		image.FillRect(new Rect2I(8, 12, 16, 12), new Color("b8a898"));
		image.FillRect(new Rect2I(12, 8, 10, 8), new Color("d9d0c4"));
		image.FillRect(new Rect2I(18, 16, 6, 8), new Color("8a8178"));
		return ImageTexture.CreateFromImage(image);
	}
}
