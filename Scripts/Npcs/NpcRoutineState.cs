namespace Vilanova;

/// <summary>
/// Coarse routine for a resident. Persisted later as this enum; no needs or jobs yet.
/// </summary>
public enum NpcRoutineState
{
	Idle,
	Wandering,
	GoingToBuilding,
	InsideBuilding,
	LeavingBuilding,
	GoingToActivity,
	AligningToActivity,
	PerformingActivity,
	LeavingActivity,
}
