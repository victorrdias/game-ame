using System;
using System.Collections.Generic;

namespace Vilanova;

/// <summary>
/// Central catalog of activity spots. All reserve/release calls run on the main thread.
/// </summary>
public sealed class NpcInteractionRegistry
{
	private readonly Dictionary<string, NpcInteractionPoint> _byId = new();
	private readonly Dictionary<string, List<string>> _bySource = new();
	private int _sessionCounter;

	public IReadOnlyCollection<NpcInteractionPoint> AllPoints => _byId.Values;

	public string Register(NpcInteractionPoint point)
	{
		if (point == null || string.IsNullOrEmpty(point.Id))
		{
			return null;
		}

		_byId[point.Id] = point;
		if (!string.IsNullOrEmpty(point.SourceInstanceId))
		{
			if (!_bySource.TryGetValue(point.SourceInstanceId, out var list))
			{
				list = new List<string>();
				_bySource[point.SourceInstanceId] = list;
			}

			list.Add(point.Id);
		}

		return point.Id;
	}

	public void UnregisterBySource(string sourceInstanceId)
	{
		if (string.IsNullOrEmpty(sourceInstanceId) || !_bySource.TryGetValue(sourceInstanceId, out var ids))
		{
			return;
		}

		foreach (var id in ids)
		{
			_byId.Remove(id);
		}

		_bySource.Remove(sourceInstanceId);
	}

	public NpcInteractionPoint Find(string pointId)
	{
		return pointId != null && _byId.TryGetValue(pointId, out var point) ? point : null;
	}

	public void CollectAvailable(List<NpcInteractionPoint> results, NpcActivityType? filter = null)
	{
		results.Clear();
		foreach (var point in _byId.Values)
		{
			if (filter.HasValue && point.ActivityType != filter.Value)
			{
				continue;
			}

			if (point.IsUsable)
			{
				results.Add(point);
			}
		}
	}

	/// <summary>
	/// Atomically marks a point reserved for one NPC. Returns false if another NPC holds it.
	/// </summary>
	public bool TryReserve(NpcInteractionPoint point, string npcId)
	{
		if (point == null || string.IsNullOrEmpty(npcId) || !point.IsUsable)
		{
			return false;
		}

		if (point.State != NpcInteractionPointState.Available)
		{
			return false;
		}

		point.State = NpcInteractionPointState.Reserved;
		point.ReservedByNpcId = npcId;
		return true;
	}

	public void MarkOccupied(NpcInteractionPoint point, string npcId)
	{
		if (point == null)
		{
			return;
		}

		point.State = NpcInteractionPointState.Occupied;
		point.ReservedByNpcId = npcId;
	}

	public void Release(NpcInteractionPoint point, string npcId)
	{
		if (point == null)
		{
			return;
		}

		if (!string.IsNullOrEmpty(point.ReservedByNpcId) &&
		    !string.IsNullOrEmpty(npcId) &&
		    point.ReservedByNpcId != npcId)
		{
			return;
		}

		point.State = NpcInteractionPointState.Available;
		point.ReservedByNpcId = null;
	}

	public void InvalidateReservationsForSource(string sourceInstanceId)
	{
		if (string.IsNullOrEmpty(sourceInstanceId) || !_bySource.TryGetValue(sourceInstanceId, out var ids))
		{
			return;
		}

		foreach (var id in ids)
		{
			if (!_byId.TryGetValue(id, out var point))
			{
				continue;
			}

			point.State = NpcInteractionPointState.Available;
			point.ReservedByNpcId = null;
		}
	}

	public string NewPointId(string prefix)
	{
		_sessionCounter++;
		return $"{prefix}-{_sessionCounter}";
	}
}
