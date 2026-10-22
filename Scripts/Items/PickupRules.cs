using System;

namespace Vilanova;

public static class PickupRules
{
	public static bool CanAttract(
		float horizontal,
		float verticalGap,
		bool settled,
		double ageSeconds,
		double pickupDelay,
		float attractionRadius,
		float verticalLimit,
		bool yielded)
	{
		if (!settled || yielded)
		{
			return false;
		}

		if (ageSeconds < pickupDelay)
		{
			return false;
		}

		if (Math.Abs(verticalGap) > verticalLimit)
		{
			return false;
		}

		return horizontal >= 0f && horizontal <= attractionRadius;
	}

	public static bool CanCollect(float horizontal, float collectionRadius)
	{
		return horizontal >= 0f && horizontal <= collectionRadius;
	}
}
