using System;
using System.Collections.Generic;
using Godot;

namespace Vilanova;

public sealed class PlacedBuilding
{
	public Node3D Visual { get; }
	public BuildingDefinition Definition { get; }
	public Vector2I OriginCell { get; }
	public int RotationSteps { get; }
	public string InstanceId { get; }

	public PlacedBuilding(
		Node3D visual,
		BuildingDefinition definition,
		Vector2I originCell,
		int rotationSteps,
		string instanceId = null)
	{
		Visual = visual;
		Definition = definition;
		OriginCell = originCell;
		RotationSteps = FootprintHelper.NormalizeRotationSteps(rotationSteps);
		InstanceId = string.IsNullOrEmpty(instanceId)
			? "placed:" + Guid.NewGuid().ToString("N")
			: instanceId;
	}

	public bool CanDemolish => Definition.CanDemolish;

	public IEnumerable<Vector2I> OccupiedCells()
	{
		return FootprintHelper.EnumerateFootprint(OriginCell, Definition.SizeInCells, RotationSteps);
	}
}
