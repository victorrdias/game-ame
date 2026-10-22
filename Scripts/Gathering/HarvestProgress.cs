namespace Vilanova;

/// <summary>
/// Hit counter for one resource. Instances do not share progress.
/// </summary>
public sealed class HarvestProgress
{
	public int Hits { get; private set; }
	public int Required { get; }
	public bool IsDepleted { get; private set; }

	public HarvestProgress(int required)
	{
		Required = required < 1 ? 1 : required;
	}

	/// <summary>
	/// Registers one hit. Returns false when the resource was already depleted.
	/// </summary>
	public bool TryHit(out bool depleted)
	{
		depleted = false;
		if (IsDepleted)
		{
			return false;
		}

		Hits++;
		depleted = Hits >= Required;
		IsDepleted = depleted;
		return true;
	}
}
