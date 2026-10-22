using Godot;

namespace Vilanova;

public partial class BuildingInteractionController : Node
{
	[Export] public float InteractionRadius { get; set; } = 3.0f;
	[Export] public NodePath BuildingManagerPath { get; set; } = new("../BuildingManager");
	[Export] public NodePath PlayerPath { get; set; } = new("../Player");
	[Export] public NodePath InfoPanelPath { get; set; } = new("../BuildingInfoPanelUI");
	[Export] public NodePath IndicatorPath { get; set; } = new("../InteractionIndicator");

	private BuildingManager _buildingManager = null!;
	private PlayerController _player = null!;
	private BuildingInfoPanelUI _infoPanel = null!;
	private InteractionIndicatorVisual _indicator = null!;

	public PlacedBuilding NearestTarget { get; private set; }

	public override void _Ready()
	{
		_buildingManager = GetNode<BuildingManager>(BuildingManagerPath);
		_player = GetNode<PlayerController>(PlayerPath);
		_infoPanel = GetNode<BuildingInfoPanelUI>(InfoPanelPath);
		_indicator = GetNode<InteractionIndicatorVisual>(IndicatorPath);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("build_interact") && !_infoPanel.IsOpen)
		{
			if (NearestTarget != null && _buildingManager.Mode == BuildingMode.Idle)
			{
				var center = _buildingManager.GetBuildingFootprintCenter(NearestTarget);
				_infoPanel.ShowBuilding(NearestTarget, center);
				GetViewport().SetInputAsHandled();
			}

			return;
		}
	}

	public override void _Process(double delta)
	{
		if (_infoPanel.IsOpen || _buildingManager.Mode != BuildingMode.Idle)
		{
			NearestTarget = null;
			_indicator.HideIndicator();
			return;
		}

		NearestTarget = _buildingManager.FindNearestPlacedBuilding(_player.GlobalPosition, InteractionRadius);
		if (NearestTarget == null)
		{
			_indicator.HideIndicator();
			return;
		}

		_indicator.ShowAt(_buildingManager.GetBuildingFootprintCenter(NearestTarget));
	}
}
