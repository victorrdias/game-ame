namespace Vilanova;

/// <summary>
/// Stable identity for one resident. The node may hide inside a building, but this record stays.
/// HomeBuildingId and CurrentBuildingId refer to <see cref="PlacedBuilding.InstanceId"/>.
/// </summary>
public sealed class NpcResident
{
	public string Id { get; }
	public string DisplayName { get; }
	public string HomeBuildingId { get; set; }
	public string CurrentBuildingId { get; set; }
	public NpcRoutineState State { get; set; } = NpcRoutineState.Idle;

	public NpcResident(string id, string displayName)
	{
		Id = id;
		DisplayName = displayName;
	}
}
