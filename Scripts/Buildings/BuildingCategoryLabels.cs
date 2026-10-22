namespace Vilanova;

public static class BuildingCategoryLabels
{
	public static string ToDisplayName(BuildingCategory category)
	{
		return category switch
		{
			BuildingCategory.Buildings => "Edifícios",
			BuildingCategory.Nature => "Natureza",
			BuildingCategory.Decoration => "Decoração",
			BuildingCategory.Paths => "Caminhos",
			_ => category.ToString(),
		};
	}
}
