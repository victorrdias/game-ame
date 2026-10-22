using Godot;

namespace Vilanova;

/// <summary>
/// Short swing of a box-built axe or pickaxe, parented to the character model.
/// </summary>
public partial class GatherToolVisual : Node3D
{
	private Node3D _axe = null!;
	private Node3D _pickaxe = null!;
	private float _swingTime = 1.0f;
	private bool _playing;

	public override void _Ready()
	{
		_axe = BuildAxe();
		_pickaxe = BuildPickaxe();
		AddChild(_axe);
		AddChild(_pickaxe);
		Visible = false;
	}

	public void Play(GatherTool tool)
	{
		Visible = true;
		_axe.Visible = tool == GatherTool.Axe;
		_pickaxe.Visible = tool == GatherTool.Pickaxe;
		_swingTime = 0.0f;
		_playing = true;
		RotationDegrees = new Vector3(-55.0f, 0.0f, 0.0f);
	}

	public override void _Process(double delta)
	{
		if (!_playing)
		{
			return;
		}

		_swingTime += (float)delta;
		var duration = 0.28f;
		var t = Mathf.Clamp(_swingTime / duration, 0.0f, 1.0f);
		var angle = Mathf.Lerp(-55.0f, 68.0f, t * t);
		RotationDegrees = new Vector3(angle, 0.0f, 18.0f);
		if (t < 1.0f)
		{
			return;
		}

		_playing = false;
		Visible = false;
	}

	private static Node3D BuildAxe()
	{
		var root = new Node3D();
		root.AddChild(Bar(new Vector3(0.05f, 0.42f, 0.05f), new Vector3(0, 0.2f, 0), new Color("6f4a2e")));
		root.AddChild(Bar(new Vector3(0.2f, 0.1f, 0.06f), new Vector3(0.08f, 0.4f, 0), new Color("8d8d93")));
		return root;
	}

	private static Node3D BuildPickaxe()
	{
		var root = new Node3D();
		root.AddChild(Bar(new Vector3(0.05f, 0.46f, 0.05f), new Vector3(0, 0.22f, 0), new Color("6f4a2e")));
		var head = Bar(new Vector3(0.28f, 0.06f, 0.06f), new Vector3(0, 0.44f, 0), new Color("6e655c"));
		head.RotationDegrees = new Vector3(0, 0, 28);
		root.AddChild(head);
		return root;
	}

	private static MeshInstance3D Bar(Vector3 size, Vector3 position, Color color)
	{
		return new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size },
			Position = position,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = color,
				Roughness = 1.0f,
				Metallic = 0.0f,
				SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
			},
		};
	}
}
