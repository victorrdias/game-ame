using Godot;

namespace Vilanova;

public partial class InteractionIndicatorVisual : Node3D
{
	[Export] public float RingRadius { get; set; } = 0.55f;
	[Export] public float Height { get; set; } = 0.08f;

	private MeshInstance3D _mesh = null!;

	public override void _Ready()
	{
		_mesh = new MeshInstance3D
		{
			Mesh = new CylinderMesh
			{
				TopRadius = RingRadius,
				BottomRadius = RingRadius,
				Height = 0.06f,
			},
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		var material = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.95f, 0.88f, 0.45f, 0.72f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		};
		_mesh.MaterialOverride = material;
		_mesh.Position = new Vector3(0, Height, 0);
		AddChild(_mesh);
		Visible = false;
	}

	public void ShowAt(Vector3 worldPosition)
	{
		GlobalPosition = worldPosition;
		Visible = true;
	}

	public void HideIndicator()
	{
		Visible = false;
	}

	public override void _Process(double delta)
	{
		if (!Visible)
		{
			return;
		}

		_mesh.RotationDegrees = new Vector3(0, _mesh.RotationDegrees.Y + 48f * (float)delta, 0);
	}
}
