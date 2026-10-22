using System.Collections.Generic;
using Godot;

namespace Vilanova;

public sealed class BuildingOccupancyGrid
{
	private readonly Dictionary<Vector2I, PlacedBuilding> _structureCells = new();
	private readonly Dictionary<Vector2I, PlacedBuilding> _pathCells = new();
	private readonly HashSet<PlacedBuilding> _allBuildings = new();

	public void Register(PlacedBuilding placed)
	{
		_allBuildings.Add(placed);
		var map = GetMap(placed.Definition.Layer);
		foreach (var cell in placed.OccupiedCells())
		{
			map[cell] = placed;
		}
	}

	public void Unregister(PlacedBuilding placed)
	{
		_allBuildings.Remove(placed);
		var map = GetMap(placed.Definition.Layer);
		foreach (var cell in placed.OccupiedCells())
		{
			if (map.TryGetValue(cell, out var owner) && owner == placed)
			{
				map.Remove(cell);
			}
		}
	}

	public IEnumerable<PlacedBuilding> AllBuildings => _allBuildings;

	public bool CanPlace(BuildingDefinition definition, Vector2I originCell, int rotationSteps, GridSystem grid)
	{
		foreach (var cell in FootprintHelper.EnumerateFootprint(originCell, definition.SizeInCells, rotationSteps))
		{
			if (!grid.IsCellInBounds(cell) || CellBlocks(definition, cell))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Paths share a cell with houses and decorations. A river, or another path, still blocks them.
	/// Structures keep both layers exclusive.
	/// </summary>
	public bool CellBlocks(BuildingDefinition definition, Vector2I cell)
	{
		if (definition == null)
		{
			return true;
		}

		if (definition.Layer == OccupancyLayer.Path)
		{
			if (_pathCells.ContainsKey(cell))
			{
				return true;
			}

			return _structureCells.TryGetValue(cell, out var structure) && IsRiver(structure);
		}

		return _structureCells.ContainsKey(cell) || _pathCells.ContainsKey(cell);
	}

	public PlacedBuilding GetAtCell(Vector2I cell)
	{
		if (_structureCells.TryGetValue(cell, out var structure))
		{
			return structure;
		}

		if (_pathCells.TryGetValue(cell, out var path))
		{
			return path;
		}

		return null;
	}

	public PlacedBuilding GetOnLayer(Vector2I cell, OccupancyLayer layer)
	{
		return GetMap(layer).TryGetValue(cell, out var placed) ? placed : null;
	}

	private static bool IsRiver(PlacedBuilding placed)
	{
		return placed?.Definition != null && placed.Definition.LinkGroup == "river";
	}

	private Dictionary<Vector2I, PlacedBuilding> GetMap(OccupancyLayer layer)
	{
		return layer == OccupancyLayer.Path ? _pathCells : _structureCells;
	}
}
