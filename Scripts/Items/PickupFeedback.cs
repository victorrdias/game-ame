using Godot;

namespace Vilanova;

public static class PickupFeedback
{
	private static AudioStreamWav _pop;
	private static int _serial;

	public static void Show(Node3D host, Vector3 worldPosition, string text)
	{
		if (host == null)
		{
			return;
		}

		var side = ((_serial++ % 5) - 2) * 0.16f;
		var toast = new Label3D
		{
			Text = text,
			TopLevel = true,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			FontSize = 32,
			PixelSize = 0.004f,
			Modulate = new Color("3c291c"),
			OutlineModulate = new Color("f4e6cf"),
			OutlineSize = 8,
		};
		host.AddChild(toast);
		toast.GlobalPosition = worldPosition + new Vector3(side, 0.55f, 0.0f);
		var rise = toast.CreateTween();
		rise.SetParallel(true);
		rise.TweenProperty(toast, "position:y", toast.Position.Y + 0.65f, 0.7f);
		rise.TweenProperty(toast, "modulate:a", 0.0f, 0.7f);
		rise.Chain().TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(toast))
			{
				toast.QueueFree();
			}
		}));

		var puff = new MeshInstance3D
		{
			TopLevel = true,
			Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f },
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				AlbedoColor = new Color(0.95f, 0.9f, 0.78f, 0.85f),
			},
		};
		host.AddChild(puff);
		puff.GlobalPosition = worldPosition + new Vector3(0.0f, 0.25f, 0.0f);
		var puffTween = puff.CreateTween();
		puffTween.TweenProperty(puff, "scale", new Vector3(1.8f, 1.8f, 1.8f), 0.22f);
		puffTween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(puff))
			{
				puff.QueueFree();
			}
		}));

		var player = new AudioStreamPlayer
		{
			Stream = Pop(),
			VolumeDb = -8.0f,
		};
		host.AddChild(player);
		player.Finished += player.QueueFree;
		player.Play();
	}

	private static AudioStreamWav Pop()
	{
		if (_pop != null)
		{
			return _pop;
		}

		const int rate = 22050;
		const int count = 1400;
		var data = new byte[count * 2];
		for (var i = 0; i < count; i++)
		{
			var time = i / (float)rate;
			var envelope = 1.0f - i / (float)count;
			var wave = Mathf.Sin(time * 880.0f * Mathf.Tau) * envelope * envelope;
			var value = (short)(wave * 7000.0f);
			data[i * 2] = (byte)(value & 255);
			data[i * 2 + 1] = (byte)((value >> 8) & 255);
		}

		_pop = new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = rate,
			Stereo = false,
			Data = data,
		};
		return _pop;
	}
}
