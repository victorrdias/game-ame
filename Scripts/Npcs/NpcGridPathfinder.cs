using System;
using System.Collections.Generic;
using Godot;

namespace Vilanova;

/// <summary>
/// A* on the town occupancy grid.
/// NavigationAgent3D / NavMesh was skipped: NPCs path on the 2m visual grid, construction
/// mutates that grid immediately, and a baked navmesh would need a rebake per place or demolish.
/// Obstacle avoidance does not carve paths around new houses. Cell A* stays aligned with placement.
/// </summary>
public static class NpcGridPathfinder
{
	private static readonly Vector2I[] Neighbors =
	{
		new(1, 0),
		new(-1, 0),
		new(0, 1),
		new(0, -1),
		new(1, 1),
		new(1, -1),
		new(-1, 1),
		new(-1, -1),
	};

	public static bool TryFindPath(
		Vector2I start,
		Vector2I goal,
		Func<Vector2I, bool> isWalkable,
		List<Vector2I> path,
		Func<Vector2I, Vector2I, bool> canTraverse = null)
	{
		path.Clear();
		if (isWalkable == null || !isWalkable(start) || !isWalkable(goal))
		{
			return false;
		}

		if (start == goal)
		{
			path.Add(start);
			return true;
		}

		var open = new PriorityQueue<Vector2I, int>();
		var gScore = new Dictionary<Vector2I, int> { [start] = 0 };
		var cameFrom = new Dictionary<Vector2I, Vector2I>();
		var closed = new HashSet<Vector2I>();
		open.Enqueue(start, Heuristic(start, goal));

		var guard = 0;
		while (open.Count > 0 && guard++ < 2048)
		{
			var current = open.Dequeue();
			if (!closed.Add(current))
			{
				continue;
			}

			if (current == goal)
			{
				Reconstruct(cameFrom, current, path);
				Simplify(path);
				return path.Count > 0;
			}

			foreach (var offset in Neighbors)
			{
				var next = new Vector2I(current.X + offset.X, current.Y + offset.Y);
				if (closed.Contains(next) || !isWalkable(next) || !CanCross(canTraverse, current, next))
				{
					continue;
				}

				var diagonal = offset.X != 0 && offset.Y != 0;
				if (diagonal &&
				    (!isWalkable(new Vector2I(current.X + offset.X, current.Y)) ||
				     !isWalkable(new Vector2I(current.X, current.Y + offset.Y)) ||
				     !CanCross(canTraverse, current, new Vector2I(current.X + offset.X, current.Y)) ||
				     !CanCross(canTraverse, current, new Vector2I(current.X, current.Y + offset.Y))))
				{
					continue;
				}

				var tentative = gScore[current] + (diagonal ? 14 : 10);
				if (gScore.TryGetValue(next, out var known) && tentative >= known)
				{
					continue;
				}

				cameFrom[next] = current;
				gScore[next] = tentative;
				open.Enqueue(next, tentative + Heuristic(next, goal));
			}
		}

		return false;
	}

	private static void Reconstruct(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current, List<Vector2I> path)
	{
		path.Add(current);
		while (cameFrom.TryGetValue(current, out var previous))
		{
			current = previous;
			path.Add(current);
		}

		path.Reverse();
	}

	private static void Simplify(List<Vector2I> path)
	{
		if (path.Count < 3)
		{
			return;
		}

		var compact = new List<Vector2I>(path.Count) { path[0] };
		for (var i = 1; i < path.Count - 1; i++)
		{
			var fromKept = path[i] - compact[^1];
			var toNext = path[i + 1] - path[i];
			var cross = fromKept.X * toNext.Y - fromKept.Y * toNext.X;
			if (cross != 0)
			{
				compact.Add(path[i]);
			}
		}

		compact.Add(path[^1]);
		path.Clear();
		path.AddRange(compact);
	}

	private static bool CanCross(Func<Vector2I, Vector2I, bool> canTraverse, Vector2I from, Vector2I to)
	{
		return canTraverse == null || canTraverse(from, to);
	}

	private static int Heuristic(Vector2I a, Vector2I b)
	{
		var dx = Math.Abs(a.X - b.X);
		var dy = Math.Abs(a.Y - b.Y);
		var diagonal = Math.Min(dx, dy);
		var straight = Math.Max(dx, dy) - diagonal;
		return diagonal * 14 + straight * 10;
	}
}
