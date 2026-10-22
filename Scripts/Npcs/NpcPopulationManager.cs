using System;
using System.Collections.Generic;
using Godot;

namespace Vilanova;

public partial class NpcPopulationManager : Node
{
	[Export] public int ResidentCount { get; set; } = 5;
	[Export] public PackedScene ResidentScene { get; set; }
	[Export] public NodePath BuildingManagerPath { get; set; } = new("../BuildingManager");
	[Export] public NodePath NpcsRootPath { get; set; } = new("../Npcs");
	[Export] public NodePath PlayerPath { get; set; } = new("../Player");
	[Export] public float MoveSpeed { get; set; } = 2.05f;
	[Export] public float ArrivalRadius { get; set; } = 0.48f;
	[Export] public float MinIdleSeconds { get; set; } = 0.7f;
	[Export] public float MaxIdleSeconds { get; set; } = 2.2f;
	[Export] public float MinVisitSeconds { get; set; } = 4.0f;
	[Export] public float MaxVisitSeconds { get; set; } = 9.0f;
	[Export] public float VisitChance { get; set; } = 0.55f;
	[Export] public float RotationSmoothness { get; set; } = 12.0f;
	[Export] public float ExitRetrySeconds { get; set; } = 6.0f;

	public TownWalkability Walkability { get; private set; }
	public IReadOnlyList<NpcController> Residents => _residents;

	private readonly Random _random = new();
	private BuildingManager _buildingManager = null!;
	private Node3D _npcsRoot = null!;
	private PlayerController _player;
	private NpcRoutineConfig _config = null!;
	private bool _started;
	private const float SpawnClearance = 3.4f;
	private const float RelaxedSpawnClearance = 2.1f;
	private const float PlayerSpawnClearance = 3.6f;
	private const float StandClearance = 0.9f;

	private static readonly string[] Names =
	{
		"Ana",
		"Bruno",
		"Clara",
		"Davi",
		"Elena",
		"Felix",
		"Helena",
		"Igor",
		"Júlia",
		"Luiz",
	};

	private static readonly (Color Shirt, Color Pants, Color Skin)[] Palettes =
	{
		(new Color("c45c4a"), new Color("5c4636"), new Color("f5d5b8")),
		(new Color("4f7ea8"), new Color("3e4a58"), new Color("f0c8a0")),
		(new Color("6e8f4e"), new Color("4a3b2a"), new Color("e8c4a8")),
		(new Color("d08a3a"), new Color("5a3d32"), new Color("f6dcc4")),
		(new Color("8a5f8d"), new Color("3f3a4a"), new Color("f3d0b4")),
		(new Color("c46b7a"), new Color("4d4036"), new Color("e7be9a")),
	};

	private readonly List<NpcController> _residents = new();
	private readonly List<Vector2I> _pathScratch = new();

	public override void _Ready()
	{
		CallDeferred(nameof(StartPopulation));
	}

	public override void _ExitTree()
	{
		if (_buildingManager != null)
		{
			_buildingManager.BuildingPlaced -= OnBuildingPlaced;
			_buildingManager.BuildingRemoved -= OnBuildingRemoved;
		}
	}

	public PlacedBuilding FindBuilding(string instanceId)
	{
		return _buildingManager?.FindByInstanceId(instanceId);
	}

	public bool TryAssignHome(string residentId, string buildingInstanceId)
	{
		var resident = FindResident(residentId);
		var building = FindBuilding(buildingInstanceId);
		if (resident == null || building == null)
		{
			return false;
		}

		resident.Resident.HomeBuildingId = building.InstanceId;
		return true;
	}

	public void CollectReachableVisits(Vector2I start, List<BuildingApproach> results)
	{
		results.Clear();
		if (_buildingManager?.Occupancy == null || Walkability == null)
		{
			return;
		}

		foreach (var building in _buildingManager.Occupancy.AllBuildings)
		{
			if (building?.Definition == null || !building.Definition.IsVisitable)
			{
				continue;
			}

			if (!BuildingEntrance.TryResolve(building, Walkability, out var approach))
			{
				continue;
			}

			if (!NpcGridPathfinder.TryFindPath(start, approach.Cell, Walkability.IsWalkable, _pathScratch))
			{
				continue;
			}

			results.Add(approach);
		}
	}

	public bool IsStandClear(Vector3 world, NpcController except)
	{
		if (Walkability == null || !Walkability.IsWalkable(Walkability.Grid.WorldToCell(world)))
		{
			return false;
		}

		if (_player != null && IsInstanceValid(_player) &&
		    HorizontalReach.DistanceXZ(_player.GlobalPosition, world) < StandClearance)
		{
			return false;
		}

		foreach (var npc in _residents)
		{
			if (npc == except || !npc.BlocksPlacement)
			{
				continue;
			}

			if (HorizontalReach.DistanceXZ(npc.GlobalPosition, world) < StandClearance)
			{
				return false;
			}
		}

		return true;
	}

	public bool TryFindStandPoint(Vector3 origin, NpcController except, out Vector3 world)
	{
		world = Vector3.Zero;
		if (Walkability == null)
		{
			return false;
		}

		var start = Walkability.Grid.WorldToCell(origin);
		var queue = new Queue<Vector2I>();
		var seen = new HashSet<Vector2I>();
		queue.Enqueue(start);
		seen.Add(start);

		while (queue.Count > 0)
		{
			var cell = queue.Dequeue();
			if (Walkability.IsWalkable(cell))
			{
				var candidate = Walkability.Grid.CellToWorldCenter(cell);
				candidate.Y = 0.0f;
				if (IsStandClear(candidate, except))
				{
					world = candidate;
					return true;
				}
			}

			foreach (var offset in TownWalkability.CardinalOffsets)
			{
				var next = new Vector2I(cell.X + offset.X, cell.Y + offset.Y);
				if (!seen.Add(next) || !Walkability.Grid.IsCellInBounds(next))
				{
					continue;
				}

				var dx = Math.Abs(next.X - start.X);
				var dy = Math.Abs(next.Y - start.Y);
				if (Math.Max(dx, dy) > 8)
				{
					continue;
				}

				queue.Enqueue(next);
			}
		}

		return false;
	}

	public bool TryFindNearestWalkable(Vector3 origin, int maxRadius, out Vector2I cell)
	{
		cell = default;
		if (Walkability == null)
		{
			return false;
		}

		var start = Walkability.Grid.WorldToCell(origin);
		if (Walkability.IsWalkable(start))
		{
			cell = start;
			return true;
		}

		for (var radius = 1; radius <= maxRadius; radius++)
		{
			for (var dx = -radius; dx <= radius; dx++)
			{
				for (var dy = -radius; dy <= radius; dy++)
				{
					if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
					{
						continue;
					}

					var candidate = new Vector2I(start.X + dx, start.Y + dy);
					if (!Walkability.IsWalkable(candidate))
					{
						continue;
					}

					cell = candidate;
					return true;
				}
			}
		}

		return false;
	}

	private void StartPopulation()
	{
		if (_started || !IsInsideTree())
		{
			return;
		}

		_buildingManager = GetNodeOrNull<BuildingManager>(BuildingManagerPath);
		_npcsRoot = GetNodeOrNull<Node3D>(NpcsRootPath);
		_player = GetNodeOrNull<PlayerController>(PlayerPath);
		if (_buildingManager?.Grid == null || _buildingManager.Occupancy == null || _npcsRoot == null)
		{
			GD.PushError("NpcPopulationManager não encontrou a grade, a ocupação ou o nó Npcs.");
			return;
		}

		ResidentScene ??= GD.Load<PackedScene>("res://Scenes/Npcs/NpcResident.tscn");
		if (ResidentScene == null)
		{
			GD.PushError("Cena res://Scenes/Npcs/NpcResident.tscn ausente.");
			return;
		}

		_config = new NpcRoutineConfig
		{
			MoveSpeed = MoveSpeed,
			ArrivalRadius = ArrivalRadius,
			MinIdleSeconds = MinIdleSeconds,
			MaxIdleSeconds = MaxIdleSeconds,
			MinVisitSeconds = MinVisitSeconds,
			MaxVisitSeconds = MaxVisitSeconds,
			VisitChance = VisitChance,
			RotationSmoothness = RotationSmoothness,
			ExitRetrySeconds = ExitRetrySeconds,
		};

		Walkability = new TownWalkability(_buildingManager.Grid, _buildingManager.Occupancy);
		_buildingManager.BuildingPlaced += OnBuildingPlaced;
		_buildingManager.BuildingRemoved += OnBuildingRemoved;
		_started = true;

		LogEntrances();
		SpawnResidents();
		LogSamplePath();
		GD.Print($"População: {_residents.Count} moradores.");
	}

	private void OnBuildingPlaced(PlacedBuilding building)
	{
		foreach (var npc in _residents)
		{
			npc.OnNavigationChanged();
		}
	}

	private void OnBuildingRemoved(PlacedBuilding building)
	{
		var origin = _buildingManager.GetBuildingFootprintCenter(building);
		foreach (var npc in _residents)
		{
			npc.HandleBuildingRemoved(building.InstanceId, origin);
		}
	}

	private void SpawnResidents()
	{
		var count = Mathf.Clamp(ResidentCount, 0, 24);
		var cells = new List<Vector2I>();
		CollectSpawnCells(cells, count, SpawnClearance);
		if (cells.Count < count)
		{
			cells.Clear();
			CollectSpawnCells(cells, count, RelaxedSpawnClearance);
		}

		if (cells.Count < count)
		{
			GD.PushWarning($"Só havia {cells.Count} vagas livres para {count} moradores.");
		}

		for (var i = 0; i < cells.Count; i++)
		{
			var world = Walkability.Grid.CellToWorldCenter(cells[i]);
			world.Y = 0.0f;
			SpawnOne(i, world);
		}
	}

	private void CollectSpawnCells(List<Vector2I> chosen, int count, float minSpacing)
	{
		var candidates = new List<Vector2I>();
		var grid = Walkability.Grid;
		for (var x = 1; x < grid.CellCount - 1; x++)
		{
			for (var y = 1; y < grid.CellCount - 1; y++)
			{
				var cell = new Vector2I(x, y);
				if (!Walkability.IsWalkable(cell))
				{
					continue;
				}

				var world = grid.CellToWorldCenter(cell);
				if (_player != null && HorizontalReach.DistanceXZ(_player.GlobalPosition, world) < PlayerSpawnClearance)
				{
					continue;
				}

				candidates.Add(cell);
			}
		}

		for (var i = candidates.Count - 1; i > 0; i--)
		{
			var swap = _random.Next(i + 1);
			(candidates[i], candidates[swap]) = (candidates[swap], candidates[i]);
		}

		foreach (var cell in candidates)
		{
			var world = grid.CellToWorldCenter(cell);
			var spaced = true;
			foreach (var other in chosen)
			{
				if (HorizontalReach.DistanceXZ(world, grid.CellToWorldCenter(other)) < minSpacing)
				{
					spaced = false;
					break;
				}
			}

			if (!spaced)
			{
				continue;
			}

			chosen.Add(cell);
			if (chosen.Count == count)
			{
				return;
			}
		}
	}

	private void SpawnOne(int index, Vector3 world)
	{
		var npc = ResidentScene.Instantiate<NpcController>();
		var palette = Palettes[index % Palettes.Length];
		var visual = npc.GetNode<NpcVisual>("VisualRoot");
		visual.ShirtColor = palette.Shirt;
		visual.PantsColor = palette.Pants;
		visual.SkinColor = palette.Skin;

		var resident = new NpcResident(NewResidentId(), Names[index % Names.Length]);
		_npcsRoot.AddChild(npc);
		npc.Name = resident.DisplayName;
		npc.GlobalPosition = world;
		npc.Initialize(resident, this, _config);
		_residents.Add(npc);
	}

	private void LogEntrances()
	{
		foreach (var building in _buildingManager.Occupancy.AllBuildings)
		{
			if (building?.Definition == null || !building.Definition.IsVisitable)
			{
				continue;
			}

			if (!BuildingEntrance.TryResolve(building, Walkability, out var approach))
			{
				GD.PushWarning($"Entrada inválida para {building.Definition.DisplayName} ({building.InstanceId}).");
				continue;
			}

			var door = building.Visual.FindChild("door_panel", true, false) as Node3D;
			if (door == null)
			{
				continue;
			}

			var doorOffset = door.GlobalPosition - building.Visual.GlobalPosition;
			var entranceOffset = approach.WorldPosition - building.Visual.GlobalPosition;
			var alignment = doorOffset.X * entranceOffset.X + doorOffset.Z * entranceOffset.Z;
			if (alignment <= 0.0f)
			{
				GD.PushWarning($"A entrada de {building.Definition.DisplayName} não está do lado da porta.");
			}
		}
	}

	private void LogSamplePath()
	{
		if (_residents.Count == 0)
		{
			return;
		}

		BuildingApproach approach = default;
		var found = false;
		foreach (var building in _buildingManager.Occupancy.AllBuildings)
		{
			if (building?.Definition != null &&
			    building.Definition.IsVisitable &&
			    BuildingEntrance.TryResolve(building, Walkability, out approach))
			{
				found = true;
				break;
			}
		}

		if (!found)
		{
			GD.PushWarning("Nenhum edifício visitável com entrada livre no início.");
			return;
		}

		var npc = _residents[0];
		var start = Walkability.Grid.WorldToCell(npc.GlobalPosition);
		var reached = NpcGridPathfinder.TryFindPath(start, approach.Cell, Walkability.IsWalkable, _pathScratch);
		if (!reached)
		{
			GD.PushWarning($"Sem caminho de {npc.Resident.DisplayName} até a entrada {approach.BuildingId}.");
		}
	}

	private NpcController FindResident(string residentId)
	{
		foreach (var npc in _residents)
		{
			if (npc.Resident.Id == residentId)
			{
				return npc;
			}
		}

		return null;
	}

	private static string NewResidentId()
	{
		return "resident-" + Guid.NewGuid().ToString("N")[..8];
	}
}
