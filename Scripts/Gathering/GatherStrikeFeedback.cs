using Godot;

namespace Vilanova;

public static class GatherStrikeFeedback
{
	private static AudioStreamWav _thud;

	public static void PlayHit(Node3D visual, GatherKind kind, bool shake)
	{
		if (visual == null || !GodotObject.IsInstanceValid(visual))
		{
			return;
		}

		if (shake)
		{
			var origin = visual.Position;
			var tween = visual.CreateTween();
			tween.TweenProperty(visual, "position", origin + new Vector3(0.07f, 0.02f, 0.04f), 0.05f);
			tween.TweenProperty(visual, "position", origin, 0.09f);
		}

		var color = kind == GatherKind.Stone ? new Color("b8a898") : new Color("9c6b43");
		for (var i = 0; i < 6; i++)
		{
			var chip = new MeshInstance3D
			{
				Mesh = new BoxMesh { Size = new Vector3(0.07f, 0.07f, 0.07f) },
				Position = new Vector3(0, 0.9f, 0),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				MaterialOverride = new StandardMaterial3D
				{
					AlbedoColor = color,
					Roughness = 1.0f,
					Metallic = 0.0f,
					SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
				},
			};
			visual.AddChild(chip);
			var angle = i / 6.0f * Mathf.Tau;
			var landing = chip.Position + new Vector3(Mathf.Cos(angle) * 0.45f, 0.15f, Mathf.Sin(angle) * 0.45f);
			var peak = chip.Position + new Vector3(Mathf.Cos(angle) * 0.22f, 0.4f, Mathf.Sin(angle) * 0.22f);
			var chipTween = chip.CreateTween();
			chipTween.TweenProperty(chip, "position", peak, 0.08f);
			chipTween.TweenProperty(chip, "position", landing, 0.16f);
			chipTween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(chip))
				{
					chip.QueueFree();
				}
			}));
		}

		var player = new AudioStreamPlayer
		{
			Stream = Thud(),
			VolumeDb = -6.0f,
		};
		visual.AddChild(player);
		player.Finished += player.QueueFree;
		player.Play();
	}

	public static void PlayFell(Node3D visual)
	{
		if (visual == null || !GodotObject.IsInstanceValid(visual))
		{
			return;
		}

		var tween = visual.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(visual, "scale", new Vector3(0.08f, 0.08f, 0.08f), 0.28f);
		tween.TweenProperty(visual, "position:y", visual.Position.Y - 0.25f, 0.28f);
		tween.Chain().TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(visual))
			{
				visual.QueueFree();
			}
		}));
	}

	private static AudioStreamWav Thud()
	{
		if (_thud != null)
		{
			return _thud;
		}

		const int rate = 22050;
		const int count = 1600;
		var data = new byte[count * 2];
		var noise = 17;
		for (var i = 0; i < count; i++)
		{
			noise = noise * 1103515245 + 12345;
			var sample = ((noise >> 16) & 32767) / 32767.0f * 2.0f - 1.0f;
			var envelope = 1.0f - i / (float)count;
			var value = (short)(sample * envelope * envelope * 12000.0f);
			data[i * 2] = (byte)(value & 255);
			data[i * 2 + 1] = (byte)((value >> 8) & 255);
		}

		_thud = new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = rate,
			Stereo = false,
			Data = data,
		};
		return _thud;
	}
}
