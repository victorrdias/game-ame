namespace Vilanova;

/// <summary>
/// Picks the nearest interactable. Within 5 cm, a resource wins over inspection,
/// then the smaller instance id wins.
/// </summary>
public static class InteractionArbiter
{
	public const float TieEpsilon = 0.05f;

	public static bool IsBetter(
		float distance,
		bool isHarvest,
		string id,
		float bestDistance,
		bool bestIsHarvest,
		string bestId)
	{
		if (bestId == null)
		{
			return true;
		}

		if (distance < bestDistance - TieEpsilon)
		{
			return true;
		}

		if (distance > bestDistance + TieEpsilon)
		{
			return false;
		}

		if (isHarvest != bestIsHarvest)
		{
			return isHarvest;
		}

		return string.CompareOrdinal(id ?? string.Empty, bestId) < 0;
	}
}
