using System.Collections.Generic;
using Godot;

namespace Vilanova;

/// <summary>
/// Card images for the build menu. Order: definition thumbnail, Assets/UI/Thumbnails/{id}.png,
/// an offline render in Assets/ArtPreviews, then a painted icon. No live 3D viewport per card.
/// </summary>
public static class BuildThumbnailLibrary
{
	private const string PreviewFolder = "res://Assets/ArtPreviews/";
	private const string ThumbnailFolder = "res://Assets/UI/Thumbnails/";

	private static readonly Dictionary<string, string> PreviewFiles = new()
	{
		["reference_cottage"] = "vn_reference_cottage_iso.png",
		["timber_cottage"] = "vn_building_timber_cottage_iso.png",
		["stone_cottage"] = "vn_building_stone_cottage_iso.png",
		["school"] = "vn_building_school_iso.png",
		["hospital"] = "vn_building_hospital_iso.png",
		["townhouse"] = "vn_building_townhouse_iso.png",
		["town_gate"] = "vn_building_town_gate_iso.png",
		["oak_tree"] = "vn_nature_oak_tree_iso.png",
		["poplar_tree"] = "vn_nature_poplar_tree_iso.png",
		["flower_bush"] = "vn_nature_flower_bush_iso.png",
		["berry_bush"] = "vn_nature_berry_bush_iso.png",
		["rustic_bench"] = "vn_deco_rustic_bench_iso.png",
		["stone_bench"] = "vn_deco_stone_bench_iso.png",
		["plaza_bench"] = "vn_deco_plaza_bench_iso.png",
		["cottage_lantern"] = "vn_deco_cottage_lantern_iso.png",
		["cottage_planter"] = "vn_deco_cottage_planter_iso.png",
		["stone_well"] = "vn_deco_stone_well_iso.png",
		["wood_cart"] = "vn_deco_wood_cart_iso.png",
		["village_sign"] = "vn_deco_village_sign_iso.png",
		["path_cobble"] = "vn_path_cobble_iso.png",
		["path_cobble_2x2"] = "vn_path_cobble_2x2_iso.png",
		["path_terracotta"] = "vn_path_terracotta_iso.png",
		["path_terracotta_2x2"] = "vn_path_terracotta_2x2_iso.png",
		["path_slab"] = "vn_path_slab_iso.png",
		["path_slab_2x2"] = "vn_path_slab_2x2_iso.png",
		["rock_small"] = "vn_rock_small_iso.png",
		["rock_medium"] = "vn_rock_medium_iso.png",
		["rock_large"] = "vn_rock_large_iso.png",
		["river_3x3"] = "vn_river_3x3_iso.png",
		["river_6x4"] = "vn_river_6x4_iso.png",
		["river_10x8"] = "vn_river_10x8_iso.png",
	};

	private static readonly Dictionary<BuildingCategory, Texture2D> Placeholders = new();

	public static Texture2D Get(BuildingDefinition definition)
	{
		if (definition == null)
		{
			return Placeholder(BuildingCategory.Buildings);
		}

		if (definition.Thumbnail != null)
		{
			return definition.Thumbnail;
		}

		var custom = TryLoad(ThumbnailFolder + definition.Id + ".png");
		if (custom != null)
		{
			return custom;
		}

		if (!string.IsNullOrEmpty(definition.Id) && PreviewFiles.TryGetValue(definition.Id, out var file))
		{
			var preview = TryLoad(PreviewFolder + file);
			if (preview != null)
			{
				return preview;
			}
		}

		return Placeholder(definition.Category);
	}

	private static Texture2D TryLoad(string path)
	{
		if (!ResourceLoader.Exists(path))
		{
			return null;
		}

		return ResourceLoader.Load<Texture2D>(path);
	}

	private static Texture2D Placeholder(BuildingCategory category)
	{
		if (Placeholders.TryGetValue(category, out var cached))
		{
			return cached;
		}

		var image = Image.CreateEmpty(96, 96, false, Image.Format.Rgba8);
		image.Fill(CozyUiTheme.ParchmentDeep);
		switch (category)
		{
			case BuildingCategory.Nature:
				image.FillRect(new Rect2I(44, 58, 8, 22), VillagePalette.TrunkBrown);
				image.FillRect(new Rect2I(28, 28, 40, 34), VillagePalette.FoliageGreen);
				break;
			case BuildingCategory.Decoration:
				image.FillRect(new Rect2I(40, 22, 16, 22), VillagePalette.WoodWarm);
				image.FillRect(new Rect2I(36, 44, 24, 28), VillagePalette.WallCream);
				break;
			case BuildingCategory.Paths:
				image.FillRect(new Rect2I(18, 40, 22, 16), VillagePalette.StoneFoundation);
				image.FillRect(new Rect2I(42, 40, 22, 16), VillagePalette.TrimPaint);
				image.FillRect(new Rect2I(30, 58, 22, 16), CozyUiTheme.Terracotta);
				break;
			default:
				image.FillRect(new Rect2I(26, 44, 44, 32), VillagePalette.WallCream);
				image.FillRect(new Rect2I(22, 28, 52, 18), VillagePalette.RoofTerracotta);
				image.FillRect(new Rect2I(44, 56, 12, 20), VillagePalette.DoorWood);
				break;
		}

		var texture = ImageTexture.CreateFromImage(image);
		Placeholders[category] = texture;
		return texture;
	}
}
