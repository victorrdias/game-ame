using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace Vilanova;

public partial class BuildingManager : Node
{
	[Export] public float TerrainSize { get; set; } = TerrainHeightField.ValleySize;
	[Export] public float CellSize { get; set; } = 2.0f;
	[Export] public float BuildReachDistance { get; set; } = 8.0f;
	[Export] public BuildingCatalog Catalog { get; set; } = null!;
	[Export] public NodePath BuildingsRootPath { get; set; } = new("../PlacedBuildings");
	[Export] public NodePath PropsPath { get; set; } = new("../Props");
	[Export] public NodePath CameraPath { get; set; } = new("../CameraController/Pivot/Camera3D");
	[Export] public NodePath PlayerPath { get; set; } = new("../Player");
	[Export] public NodePath InfoPanelPath { get; set; } = new("../BuildingInfoPanelUI");

	public BuildingMode Mode { get; private set; } = BuildingMode.Idle;
	public string PlacementFeedbackText { get; private set; } = string.Empty;
	public GridSystem Grid => _layout.Visual;
	public TownGridLayout Layout => _layout;
	public TerrainHeightField Heights => _heights;
	public BuildingOccupancyGrid Occupancy => _occupancy;

	public event Action<PlacedBuilding> BuildingPlaced;
	public event Action<PlacedBuilding> BuildingRemoved;

	private TownGridLayout _layout = null!;
	private TerrainHeightField _heights = null!;
	private BuildingOccupancyGrid _occupancy = new();
	private Node3D _buildingsRoot = null!;
	private Camera3D _camera = null!;
	private PlayerController _player = null!;
	private BuildingInfoPanelUI _infoPanel;
	private FootprintHighlightVisual _footprintHighlight = null!;
	private Node3D _previewRoot;
	private BuildingDefinition _activeDefinition;
	private bool _hasPreviewRoot;
	private bool _hasActiveDefinition;
	private Vector2I _hoverOriginCell;
	private Vector2I _hoverCenterCell;
	private int _rotationSteps;
	private bool _hoverValid;
	private bool _lastPreviewValid;
	private PlacementBlockReason _hoverBlockReason = PlacementBlockReason.None;
	private PlacedBuilding _demolishHoverTarget;
	private bool _demolishHoverValid;
	private bool _hasStrokeCell;
	private Vector2I _lastStrokeCell;
	private readonly List<Aabb> _residentBounds = new();
	private readonly HashSet<string> _releasedIds = new();
	private ulong _layoutNoticeUntilMs;

	private static readonly Color ValidFootprintColor = new(0.35f, 0.92f, 0.48f, 0.38f);
	private static readonly Color InvalidFootprintColor = new(0.95f, 0.32f, 0.32f, 0.42f);
	private static readonly Color DemolishFootprintColor = new(0.98f, 0.62f, 0.28f, 0.4f);

	public override void _Ready()
	{
		_layout = new TownGridLayout(TerrainSize, CellSize);
		_heights = TerrainHeightField.CreateValley(TerrainSize, CellSize);
		_buildingsRoot = GetNode<Node3D>(BuildingsRootPath);
		_camera = GetNode<Camera3D>(CameraPath);
		_player = GetNode<PlayerController>(PlayerPath);
		_infoPanel = GetNodeOrNull<BuildingInfoPanelUI>(InfoPanelPath);

		_footprintHighlight = new FootprintHighlightVisual
		{
			CellSize = _layout.PlacementCellSize,
			TerrainSize = TerrainSize,
		};
		_footprintHighlight.HeightAt = cell =>
		{
			var center = _layout.Placement.CellToWorldCenter(cell);
			return _heights.SampleY(center.X, center.Z);
		};
		_buildingsRoot.AddChild(_footprintHighlight);

		RegisterInitialSceneObjects();
		SpawnStarterPlaza();
		RefreshAllLinks();
		SetMode(BuildingMode.Idle);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsEcho())
		{
			return;
		}

		if (@event.IsActionPressed("export_layout"))
		{
			SavePlacedLayout();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (@event.IsActionPressed("build_cancel") && _infoPanel != null && _infoPanel.IsOpen)
		{
			return;
		}

		if (@event.IsActionPressed("build_demolish_mode"))
		{
			EnterDemolishMode();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("build_cancel"))
		{
			SetMode(BuildingMode.Idle);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("build_rotate") && Mode == BuildingMode.Placing && _hasActiveDefinition &&
		    _activeDefinition.CanRotate)
		{
			_rotationSteps = FootprintHelper.NormalizeRotationSteps(_rotationSteps + 1);
			RefreshPlacementState();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (Mode == BuildingMode.Idle || IsPointerOverUi())
		{
			return;
		}

		if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
		{
			if (mouseButton.ButtonIndex == MouseButton.Right)
			{
				SetMode(BuildingMode.Idle);
				GetViewport().SetInputAsHandled();
				return;
			}

			if (mouseButton.ButtonIndex != MouseButton.Left)
			{
				return;
			}

			if (Mode == BuildingMode.Placing)
			{
				TryPlaceBuilding();
			}
			else if (Mode == BuildingMode.Demolishing)
			{
				TryDemolishBuilding();
			}

			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		switch (Mode)
		{
			case BuildingMode.Placing when _hasActiveDefinition:
				UpdatePlacementPreview();
				ContinuePathStroke();
				break;
			case BuildingMode.Demolishing:
				UpdateDemolishPreview();
				break;
			default:
				_footprintHighlight.HideHighlight();
				if (Time.GetTicksMsec() >= _layoutNoticeUntilMs)
				{
					ShowPlacementFeedback(string.Empty);
				}

				break;
		}
	}

	public void BeginPlacing(BuildingDefinition definition)
	{
		if (definition?.VisualScene == null)
		{
			GD.PushWarning("Building definition or visual scene is missing.");
			return;
		}

		_infoPanel?.Close();
		_activeDefinition = definition;
		_hasActiveDefinition = true;
		_rotationSteps = 0;
		SetMode(BuildingMode.Placing);
	}

	public void EnterDemolishMode()
	{
		_infoPanel?.Close();
		SetMode(BuildingMode.Demolishing);
	}

	public BuildingDefinition FindDefinition(string id)
	{
		if (Catalog?.Items == null)
		{
			return null;
		}

		foreach (var item in Catalog.Items)
		{
			if (item != null && item.Id == id)
			{
				return item;
			}
		}

		return null;
	}

	public Vector3 GetBuildingFootprintCenter(PlacedBuilding building)
	{
		var rotatedSize = FootprintHelper.GetRotatedSize(building.Definition.SizeInCells, building.RotationSteps);
		return _heights.WithSurface(_layout.Placement.FootprintCenterWorld(building.OriginCell, rotatedSize));
	}

	public PlacedBuilding FindNearestPlacedBuilding(Vector3 fromWorldPosition, float maxRadius)
	{
		PlacedBuilding nearest = null;
		var bestDistance = maxRadius;

		foreach (var building in _occupancy.AllBuildings)
		{
			if (building?.Definition == null)
			{
				continue;
			}

			var center = GetBuildingFootprintCenter(building);
			var distance = HorizontalReach.DistanceXZ(fromWorldPosition, center);
			if (distance <= bestDistance)
			{
				bestDistance = distance;
				nearest = building;
			}
		}

		return nearest;
	}

	public PlacedBuilding FindByInstanceId(string instanceId)
	{
		if (string.IsNullOrEmpty(instanceId))
		{
			return null;
		}

		foreach (var building in _occupancy.AllBuildings)
		{
			if (building != null && building.InstanceId == instanceId)
			{
				return building;
			}
		}

		return null;
	}

	public string GetModeHintText()
	{
		return Mode switch
		{
			BuildingMode.Placing => _activeDefinition.Category == BuildingCategory.Paths
				? $"Construindo: {_activeDefinition.DisplayName} ({GetFootprintLabel()}) | Segure o esquerdo e ande para pintar | R: girar | Direito/Esc: cancelar"
				: $"Construindo: {_activeDefinition.DisplayName} ({GetFootprintLabel()}) | R: girar | Clique esquerdo: colocar | Direito/Esc: cancelar",
			BuildingMode.Demolishing =>
				"Demolir | Clique esquerdo: remover | Direito/Esc: cancelar",
			_ =>
				"WASD: personagem | Q/E ou botão direito: girar câmera | Scroll: zoom | U: menu | X: demolir | F: interagir | F8: gravar layout",
		};
	}

	private string GetFootprintLabel()
	{
		var size = FootprintHelper.GetRotatedSize(_activeDefinition.SizeInCells, _rotationSteps);
		return $"{size.X}x{size.Y} subtiles";
	}

	private void SetMode(BuildingMode mode)
	{
		Mode = mode;
		ClearPreview();
		_hasStrokeCell = false;
		_demolishHoverTarget = null;
		_demolishHoverValid = false;
		PlacementFeedbackText = string.Empty;
		_footprintHighlight.HideHighlight();

		if (Mode != BuildingMode.Placing)
		{
			_hasActiveDefinition = false;
		}
		else if (_hasActiveDefinition)
		{
			CreatePreview(_activeDefinition);
		}
	}

	private void UpdatePlacementPreview()
	{
		if (!_hasPreviewRoot || !_hasActiveDefinition)
		{
			return;
		}

		if (IsPointerOverUi())
		{
			_previewRoot.Visible = false;
			_footprintHighlight.HideHighlight();
			ShowPlacementFeedback(string.Empty);
			return;
		}

		if (!TryGetGroundHit(out var hit))
		{
			_previewRoot.Visible = false;
			_footprintHighlight.HideHighlight();
			ShowPlacementFeedback("Aponte para o terreno.");
			return;
		}

		_previewRoot.Visible = true;
		_hoverCenterCell = _layout.WorldToPlacement(hit);
		var rotatedSize = FootprintHelper.GetRotatedSize(_activeDefinition.SizeInCells, _rotationSteps);
		_hoverOriginCell = FootprintHelper.OriginFromCenterCell(_hoverCenterCell, rotatedSize);
		RefreshPlacementState();

		_previewRoot.GlobalPosition = GroundedFootprintCenter(_hoverOriginCell, rotatedSize);
		_previewRoot.RotationDegrees = new Vector3(0, FootprintHelper.RotationDegrees(_rotationSteps), 0);

		if (_hoverValid != _lastPreviewValid)
		{
			BuildingGhostVisual.ApplyGhostLook(_previewRoot, _hoverValid);
			_lastPreviewValid = _hoverValid;
		}

		_footprintHighlight.ShowRotatedFootprint(
			_hoverOriginCell,
			_activeDefinition.SizeInCells,
			_rotationSteps,
			_hoverValid ? ValidFootprintColor : InvalidFootprintColor);
	}

	private void ContinuePathStroke()
	{
		if (_activeDefinition.Category != BuildingCategory.Paths)
		{
			return;
		}

		if (!Input.IsMouseButtonPressed(MouseButton.Left))
		{
			_hasStrokeCell = false;
			return;
		}

		if (IsPointerOverUi() || !_hoverValid || (_hasStrokeCell && _hoverOriginCell == _lastStrokeCell))
		{
			return;
		}

		_lastStrokeCell = _hoverOriginCell;
		_hasStrokeCell = true;
		TryPlaceBuilding();
	}

	private void RefreshPlacementState()
	{
		CollectResidentBounds();
		_hoverBlockReason = BuildPlacementEvaluator.EvaluatePlacement(
			_activeDefinition,
			_hoverOriginCell,
			_rotationSteps,
			_layout.Placement,
			_occupancy,
			_heights,
			_layout.PlacementToVisual,
			_player.GlobalPosition,
			BuildReachDistance,
			_player.GetOccupancyBounds(),
			_residentBounds);
		_hoverValid = _hoverBlockReason == PlacementBlockReason.None;
		ShowPlacementFeedback(_hoverValid
			? "Posição válida."
			: BuildPlacementEvaluator.DescribeReason(_hoverBlockReason));
	}

	private void UpdateDemolishPreview()
	{
		if (IsPointerOverUi())
		{
			_footprintHighlight.HideHighlight();
			ShowPlacementFeedback(string.Empty);
			_demolishHoverTarget = null;
			_demolishHoverValid = false;
			return;
		}

		if (!TryGetGroundHit(out var hit))
		{
			_footprintHighlight.HideHighlight();
			ShowPlacementFeedback("Aponte para uma construção.");
			_demolishHoverTarget = null;
			_demolishHoverValid = false;
			return;
		}

		var cell = _layout.WorldToPlacement(hit);
		_demolishHoverTarget = _occupancy.GetAtCell(cell);
		if (_demolishHoverTarget == null || !_demolishHoverTarget.CanDemolish)
		{
			_footprintHighlight.HideHighlight();
			ShowPlacementFeedback(_demolishHoverTarget == null
				? "Nada para demolir aqui."
				: "Este item não pode ser demolido.");
			_demolishHoverValid = false;
			return;
		}

		var center = GetBuildingFootprintCenter(_demolishHoverTarget);
		_demolishHoverValid = BuildPlacementEvaluator.IsWithinBuildReach(
			_player.GlobalPosition,
			center,
			BuildReachDistance);

		ShowPlacementFeedback(_demolishHoverValid
			? $"Demolir: {_demolishHoverTarget.Definition.DisplayName}"
			: BuildPlacementEvaluator.DescribeReason(PlacementBlockReason.TooFar));

		var color = _demolishHoverValid ? DemolishFootprintColor : InvalidFootprintColor;
		_footprintHighlight.ShowRotatedFootprint(
			_demolishHoverTarget.OriginCell,
			_demolishHoverTarget.Definition.SizeInCells,
			_demolishHoverTarget.RotationSteps,
			color);
	}

	private void ShowPlacementFeedback(string text)
	{
		if (Time.GetTicksMsec() < _layoutNoticeUntilMs)
		{
			return;
		}

		PlacementFeedbackText = text;
	}

	private void SavePlacedLayout()
	{
		var pieces = new List<PlacedLayoutWriter.Piece>();
		foreach (var placed in _occupancy.AllBuildings)
		{
			if (placed?.Definition == null)
			{
				continue;
			}

			pieces.Add(new PlacedLayoutWriter.Piece
			{
				InstanceId = placed.InstanceId,
				DefinitionId = placed.Definition.Id,
				CellX = placed.OriginCell.X,
				CellZ = placed.OriginCell.Y,
				RotationSteps = placed.RotationSteps,
			});
		}

		var text = PlacedLayoutWriter.Format(pieces);
		var path = ProjectSettings.GlobalizePath("res://" + PlacedLayoutWriter.RelativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, text);
		var count = 0;
		foreach (var piece in pieces)
		{
			if (piece.InstanceId != null && piece.InstanceId.StartsWith("placed:", StringComparison.Ordinal))
			{
				count++;
			}
		}

		PlacementFeedbackText = count == 0
			? "Nenhuma peça nova para gravar."
			: $"{count} peças gravadas em {PlacedLayoutWriter.RelativePath}.";
		_layoutNoticeUntilMs = Time.GetTicksMsec() + 6000;
		GD.Print(PlacementFeedbackText);
	}

	private void TryPlaceBuilding()
	{
		if (!_hasActiveDefinition)
		{
			return;
		}

		RefreshPlacementState();
		if (!_hoverValid)
		{
			return;
		}

		var visual = _activeDefinition.VisualScene.Instantiate<Node3D>();
		_buildingsRoot.AddChild(visual);
		var rotatedSize = FootprintHelper.GetRotatedSize(_activeDefinition.SizeInCells, _rotationSteps);
		visual.GlobalPosition = GroundedFootprintCenter(_hoverOriginCell, rotatedSize);
		visual.RotationDegrees = new Vector3(0, FootprintHelper.RotationDegrees(_rotationSteps), 0);

		var placed = new PlacedBuilding(visual, _activeDefinition, _hoverOriginCell, _rotationSteps);
		visual.SetMeta("building_instance_id", placed.InstanceId);
		_occupancy.Register(placed);
		StructureCollisionBuilder.EnsureStructureCollision(visual, _activeDefinition, _rotationSteps, _layout.PlacementCellSize);
		HarvestableResource.Ensure(placed, this);
		RefreshLinkedRivers(placed.OccupiedCells());
		BuildingPlaced?.Invoke(placed);
		BuildingVisualSpawnAnimation.Play(visual);
	}

	private void TryDemolishBuilding()
	{
		if (_demolishHoverTarget == null || !_demolishHoverValid)
		{
			return;
		}

		var building = _demolishHoverTarget;
		_demolishHoverTarget = null;
		_demolishHoverValid = false;
		ReleaseFootprint(building);
		if (building.Visual != null && GodotObject.IsInstanceValid(building.Visual))
		{
			building.Visual.QueueFree();
		}
	}

	public void ReleaseFootprint(PlacedBuilding building)
	{
		if (building == null || string.IsNullOrEmpty(building.InstanceId) || !_releasedIds.Add(building.InstanceId))
		{
			return;
		}

		var touched = new List<Vector2I>(building.OccupiedCells());
		if (building.Visual != null && GodotObject.IsInstanceValid(building.Visual))
		{
			StructureCollisionBuilder.DisableCollisionRecursively(building.Visual);
		}

		_occupancy.Unregister(building);
		RefreshLinkedRivers(touched);
		BuildingRemoved?.Invoke(building);
	}

	private void RefreshLinkedRivers(IEnumerable<Vector2I> cells)
	{
		var seen = new HashSet<PlacedBuilding>();
		foreach (var cell in cells)
		{
			TryRefreshLink(cell, seen);
			TryRefreshLink(cell + new Vector2I(1, 0), seen);
			TryRefreshLink(cell + new Vector2I(-1, 0), seen);
			TryRefreshLink(cell + new Vector2I(0, 1), seen);
			TryRefreshLink(cell + new Vector2I(0, -1), seen);
		}
	}

	private void TryRefreshLink(Vector2I cell, HashSet<PlacedBuilding> seen)
	{
		RefreshLinkedPiece(_occupancy.GetOnLayer(cell, OccupancyLayer.Structure), seen);
		RefreshLinkedPiece(_occupancy.GetOnLayer(cell, OccupancyLayer.Path), seen);
	}

	private void RefreshLinkedPiece(PlacedBuilding placed, HashSet<PlacedBuilding> seen)
	{
		if (placed == null || !seen.Add(placed))
		{
			return;
		}

		ApplyLink(placed);
	}

	private void CreatePreview(BuildingDefinition definition)
	{
		ClearPreview();
		_lastPreviewValid = true;
		_previewRoot = definition.VisualScene.Instantiate<Node3D>();
		_buildingsRoot.AddChild(_previewRoot);
		StructureCollisionBuilder.DisableCollisionRecursively(_previewRoot);
		_hasPreviewRoot = true;
		CallDeferred(nameof(ApplyInitialGhostLook));
	}

	private void ApplyInitialGhostLook()
	{
		if (!_hasPreviewRoot)
		{
			return;
		}

		BuildingGhostVisual.ApplyGhostLook(_previewRoot, _hoverValid);
		_lastPreviewValid = _hoverValid;
	}

	private void ClearPreview()
	{
		if (_hasPreviewRoot && IsInstanceValid(_previewRoot))
		{
			_previewRoot.QueueFree();
		}

		_hasPreviewRoot = false;
	}

	private void SpawnStarterPlaza()
	{
		var props = GetNodeOrNull<Node3D>(PropsPath);
		if (props == null)
		{
			return;
		}

		var index = 0;
		foreach (var piece in StarterPlaza.Pieces())
		{
			var definition = FindDefinition(piece.DefinitionId);
			if (definition?.VisualScene == null)
			{
				GD.PushError($"Praça inicial: modelo ausente ({piece.DefinitionId}).");
				continue;
			}

			if (!_occupancy.CanPlace(definition, piece.OriginCell, piece.RotationSteps, _layout.Placement))
			{
				GD.PushError($"Praça inicial: sem espaço para {piece.DefinitionId} em {piece.OriginCell}.");
				continue;
			}

			var visual = definition.VisualScene.Instantiate<Node3D>();
			visual.Name = $"{piece.DefinitionId}_{piece.OriginCell.X}_{piece.OriginCell.Y}_{index}";
			props.AddChild(visual);
			var size = FootprintHelper.GetRotatedSize(definition.SizeInCells, piece.RotationSteps);
			visual.GlobalPosition = GroundedFootprintCenter(piece.OriginCell, size);
			visual.RotationDegrees = new Vector3(0.0f, piece.YawDegrees, 0.0f);
			RegisterPlacedNode(visual, definition, piece.OriginCell, piece.RotationSteps, "plaza:" + visual.Name);
			index++;
		}
	}

	private void RefreshAllLinks()
	{
		foreach (var placed in _occupancy.AllBuildings)
		{
			if (placed.Visual == null || !GodotObject.IsInstanceValid(placed.Visual))
			{
				continue;
			}

			ApplyLink(placed);
		}
	}

	private void ApplyLink(PlacedBuilding placed)
	{
		if (placed?.Visual == null || !GodotObject.IsInstanceValid(placed.Visual) || placed.Definition == null)
		{
			return;
		}

		var layer = placed.Definition.Layer;
		placed.Visual.GetNodeOrNull<RiverLink>("RiverLink")?.Apply(
			placed,
			cell => _occupancy.GetOnLayer(cell, layer),
			_layout.Placement.WorldToCell);
	}

	private void RegisterInitialSceneObjects()
	{
		var props = GetNodeOrNull(PropsPath);
		if (props == null)
		{
			return;
		}

		TryRegisterProp(props, "ReferenceCottage", "reference_cottage");
		TryRegisterProp(props, "TimberCottage", "timber_cottage");
		TryRegisterProp(props, "StoneCottage", "stone_cottage");
		TryRegisterProp(props, "OakNearLane", "oak_tree");
		TryRegisterProp(props, "OakSouth", "oak_tree");
		TryRegisterProp(props, "PoplarEast", "poplar_tree");
		TryRegisterProp(props, "FlowerBush", "flower_bush");
		TryRegisterProp(props, "BerryBush", "berry_bush");
	}

	private void TryRegisterProp(Node props, string nodeName, string definitionId)
	{
		var node = props.GetNodeOrNull<Node3D>(nodeName);
		var definition = FindDefinition(definitionId);
		if (node == null || definition == null)
		{
			return;
		}

		var origin = _layout.Placement.OriginCellFromWorldCenter(node.GlobalPosition, definition.SizeInCells);
		RegisterPlacedNode(node, definition, origin, 0, "scene:" + node.GetPath());
	}

	private void RegisterPlacedNode(
		Node3D node,
		BuildingDefinition definition,
		Vector2I originCell,
		int rotationSteps,
		string instanceId)
	{
		var placed = new PlacedBuilding(node, definition, originCell, rotationSteps, instanceId);
		node.SetMeta("building_instance_id", placed.InstanceId);
		_occupancy.Register(placed);
		StructureCollisionBuilder.EnsureStructureCollision(node, definition, rotationSteps, _layout.PlacementCellSize);
		HarvestableResource.Ensure(placed, this);
	}

	private void CollectResidentBounds()
	{
		_residentBounds.Clear();
		var tree = GetTree();
		if (tree == null)
		{
			return;
		}

		foreach (var node in tree.GetNodesInGroup(NpcController.GroupName))
		{
			if (node is NpcController npc && npc.BlocksPlacement)
			{
				_residentBounds.Add(npc.GetOccupancyBounds());
			}
		}
	}

	private Vector3 GroundedFootprintCenter(Vector2I originCell, Vector2I rotatedSize)
	{
		var world = _layout.Placement.FootprintCenterWorld(originCell, rotatedSize);
		if (_heights.EvaluateFootprint(
			    FootprintHelper.EnumerateFootprint(originCell, rotatedSize, 0),
			    _layout.PlacementToVisual,
			    out var height) == TerrainFootprintStatus.Ok)
		{
			world.Y = height;
			return world;
		}

		return _heights.WithSurface(world);
	}

	private bool TryGetGroundHit(out Vector3 hitPoint)
	{
		return TerrainMousePicker.TryPickGroundPoint(GetViewport(), _camera, out hitPoint);
	}

	private bool IsPointerOverUi()
	{
		return GetViewport().GuiGetHoveredControl() != null;
	}
}
