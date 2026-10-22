using System;
using Godot;

namespace Vilanova;

/// <summary>
/// One seat or fishing spot for the session. Reservation is owned by <see cref="NpcInteractionRegistry"/>.
/// </summary>
public sealed class NpcInteractionPoint
{
	public string Id { get; }
	public NpcActivityType ActivityType { get; }
	public Vector3 ApproachWorld { get; }
	public Vector2I ApproachCell { get; }
	public Vector3 ActivityWorld { get; }
	public float ActivityYawRadians { get; }
	public int Capacity { get; }
	public Node SourceNode { get; }
	public string SourceInstanceId { get; }
	public Func<bool> IsValid { get; }

	public NpcInteractionPointState State { get; internal set; } = NpcInteractionPointState.Available;
	public string ReservedByNpcId { get; internal set; }

	public NpcInteractionPoint(
		string id,
		NpcActivityType activityType,
		Vector3 approachWorld,
		Vector2I approachCell,
		Vector3 activityWorld,
		float activityYawRadians,
		Node sourceNode,
		string sourceInstanceId,
		Func<bool> isValid,
		int capacity = 1)
	{
		Id = id;
		ActivityType = activityType;
		ApproachWorld = approachWorld;
		ApproachCell = approachCell;
		ActivityWorld = activityWorld;
		ActivityYawRadians = activityYawRadians;
		Capacity = Math.Max(1, capacity);
		SourceNode = sourceNode;
		SourceInstanceId = sourceInstanceId ?? string.Empty;
		IsValid = isValid ?? (() => true);
	}

	public bool IsUsable => State == NpcInteractionPointState.Available && IsValid();
}
