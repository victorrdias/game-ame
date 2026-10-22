namespace Vilanova;

public static class ItemIds
{
	public const string Wood = "wood";
	public const string Stone = "stone";

	public static string ForGatherKind(GatherKind kind)
	{
		return kind == GatherKind.Stone ? Stone : Wood;
	}
}
