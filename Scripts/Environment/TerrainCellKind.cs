namespace Vilanova;

public enum TerrainCellKind
{
	Plateau,
	Ramp,
	Rim,
}

public enum TerrainRampDirection
{
	None,
	PositiveX,
	NegativeX,
	PositiveZ,
	NegativeZ,
}

public enum TerrainFootprintStatus
{
	Ok,
	Uneven,
	OutOfPlay,
}
