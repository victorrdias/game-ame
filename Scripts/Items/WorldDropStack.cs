namespace Vilanova;

public enum WorldDropState
{
	Available,
	Attracted,
	Collected,
}

/// <summary>
/// Quantity sitting on one world drop. Taking never removes more than the inventory accepted.
/// </summary>
public sealed class WorldDropStack
{
	public string ItemId { get; set; } = string.Empty;
	public int Quantity { get; set; }
	public WorldDropState State { get; set; } = WorldDropState.Available;
	public bool Yielded { get; set; }

	public int Take(int accepted)
	{
		if (State == WorldDropState.Collected || accepted <= 0 || Quantity <= 0)
		{
			return 0;
		}

		var taken = accepted < Quantity ? accepted : Quantity;
		Quantity -= taken;
		if (Quantity <= 0)
		{
			Quantity = 0;
			State = WorldDropState.Collected;
			return taken;
		}

		State = WorldDropState.Available;
		Yielded = true;
		return taken;
	}

	public void ReleaseYield()
	{
		Yielded = false;
	}
}
