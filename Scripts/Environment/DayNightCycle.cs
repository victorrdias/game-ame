using Godot;

namespace Vilanova;

/// <summary>
/// Turns the town sky through a full day. Twelve game hours last twelve real minutes.
/// </summary>
public partial class DayNightCycle : Node
{
	public const float RealSecondsPerGameHour = 60.0f;

	[Export] public float StartHour { get; set; } = 10.0f;
	[Export] public NodePath SunPath { get; set; } = new("../Sun");
	[Export] public NodePath EnvironmentPath { get; set; } = new("../WorldEnvironment");

	public float Hour => _hour;

	private float _hour;
	private DirectionalLight3D _sun;
	private DirectionalLight3D _moon;
	private WorldEnvironment _world;
	private ProceduralSkyMaterial _sky;

	public override void _Ready()
	{
		_sun = GetNode<DirectionalLight3D>(SunPath);
		_world = GetNode<WorldEnvironment>(EnvironmentPath);
		DuplicateSky();
		_moon = new DirectionalLight3D
		{
			Name = "Moon",
			ShadowEnabled = false,
			LightColor = new Color(0.62f, 0.74f, 1.0f),
			LightAngularDistance = 0.35f,
		};
		_hour = Mathf.PosMod(StartHour, 24.0f);
		Apply(Sample(_hour));
		Callable.From(AttachMoon).CallDeferred();
	}

	public override void _Process(double delta)
	{
		_hour = Advance(_hour, (float)delta);
		Apply(Sample(_hour));
	}

	public static float Advance(float hour, float deltaSeconds)
	{
		return Mathf.PosMod(hour + deltaSeconds / RealSecondsPerGameHour, 24.0f);
	}

	public static DayLightSample Sample(float hour)
	{
		hour = Mathf.PosMod(hour, 24.0f);
		var key = Evaluate(hour);
		var elevation = Mathf.Sin((hour - 6.0f) / 24.0f * Mathf.Tau);
		var daylight = Mathf.SmoothStep(-0.05f, 0.22f, elevation);
		return new DayLightSample(
			elevation,
			daylight,
			key.SkyTop,
			key.SkyHorizon,
			key.GroundBottom,
			key.GroundHorizon,
			key.Ambient,
			key.AmbientEnergy,
			key.Sun,
			key.SunEnergy * daylight,
			key.MoonEnergy * (1.0f - daylight),
			-elevation * 70.0f,
			(hour - 6.0f) / 24.0f * 360.0f);
	}

	private void AttachMoon()
	{
		if (_moon.GetParent() == null)
		{
			AddChild(_moon);
		}
	}

	private void Apply(DayLightSample sample)
	{
		_sun.RotationDegrees = new Vector3(sample.SunPitch, sample.SunYaw, 0.0f);
		_sun.LightColor = sample.SunColor;
		_sun.LightEnergy = sample.SunEnergy;
		_moon.RotationDegrees = new Vector3(-sample.SunPitch, sample.SunYaw + 180.0f, 0.0f);
		_moon.LightEnergy = sample.MoonEnergy;
		_moon.Visible = sample.MoonEnergy > 0.01f;

		if (_sky == null)
		{
			return;
		}

		_sky.SkyTopColor = sample.SkyTop;
		_sky.SkyHorizonColor = sample.SkyHorizon;
		_sky.GroundBottomColor = sample.GroundBottom;
		_sky.GroundHorizonColor = sample.GroundHorizon;
		_world.Environment.AmbientLightColor = sample.Ambient;
		_world.Environment.AmbientLightEnergy = sample.AmbientEnergy;
	}

	private void DuplicateSky()
	{
		if (_world.Environment == null)
		{
			return;
		}

		var environment = (Environment)_world.Environment.Duplicate();
		_world.Environment = environment;
		if (environment.Sky == null || environment.Sky.SkyMaterial is not ProceduralSkyMaterial)
		{
			return;
		}

		var sky = (Sky)environment.Sky.Duplicate();
		_sky = (ProceduralSkyMaterial)sky.SkyMaterial.Duplicate();
		sky.SkyMaterial = _sky;
		environment.Sky = sky;
	}

	private static LightKey Evaluate(float hour)
	{
		for (var i = 0; i < Keys.Length - 1; i++)
		{
			var from = Keys[i];
			var to = Keys[i + 1];
			if (hour < from.Hour || hour > to.Hour)
			{
				continue;
			}

			var span = to.Hour - from.Hour;
			var t = span <= 0.001f ? 0.0f : (hour - from.Hour) / span;
			t = t * t * (3.0f - 2.0f * t);
			return LightKey.Lerp(from, to, t);
		}

		return Keys[0];
	}

	private static readonly LightKey[] Keys =
	{
		new(0.0f, "0b1024", "24263f", "070910", "12141f", "5c6c96", 0.22f, "ffbf73", 0.4f, 0.34f),
		new(5.0f, "2a335c", "d97462", "1a1420", "5a3340", "c48a78", 0.2f, "ff8c52", 0.7f, 0.16f),
		new(6.5f, "6c8cc4", "f29a66", "3a4038", "c47a58", "f2b48a", 0.26f, "ff9e60", 0.95f, 0.04f),
		new(10.0f, "85b8eb", "dbe0d1", "6b7f61", "adc299", "faf0db", 0.42f, "fff2db", 1.2f, 0.0f),
		new(15.0f, "7eb0e0", "f0c8a0", "6a7058", "c4a078", "f2d2b0", 0.27f, "ffd2a0", 1.0f, 0.0f),
		new(18.0f, "4a568c", "f26b48", "3a3038", "c45a48", "e89070", 0.22f, "ff7a48", 0.75f, 0.08f),
		new(20.5f, "14182e", "4a3058", "0c0e16", "221828", "6a7098", 0.2f, "ffbf80", 0.3f, 0.3f),
		new(24.0f, "0b1024", "24263f", "070910", "12141f", "5c6c96", 0.22f, "ffbf73", 0.4f, 0.34f),
	};

	private readonly struct LightKey
	{
		public LightKey(
			float hour,
			string skyTop,
			string skyHorizon,
			string groundBottom,
			string groundHorizon,
			string ambient,
			float ambientEnergy,
			string sun,
			float sunEnergy,
			float moonEnergy)
		{
			Hour = hour;
			SkyTop = new Color(skyTop);
			SkyHorizon = new Color(skyHorizon);
			GroundBottom = new Color(groundBottom);
			GroundHorizon = new Color(groundHorizon);
			Ambient = new Color(ambient);
			AmbientEnergy = ambientEnergy;
			Sun = new Color(sun);
			SunEnergy = sunEnergy;
			MoonEnergy = moonEnergy;
		}

		public float Hour { get; }
		public Color SkyTop { get; }
		public Color SkyHorizon { get; }
		public Color GroundBottom { get; }
		public Color GroundHorizon { get; }
		public Color Ambient { get; }
		public float AmbientEnergy { get; }
		public Color Sun { get; }
		public float SunEnergy { get; }
		public float MoonEnergy { get; }

		public static LightKey Lerp(LightKey from, LightKey to, float t)
		{
			return new LightKey(
				Mathf.Lerp(from.Hour, to.Hour, t),
				from.SkyTop.Lerp(to.SkyTop, t),
				from.SkyHorizon.Lerp(to.SkyHorizon, t),
				from.GroundBottom.Lerp(to.GroundBottom, t),
				from.GroundHorizon.Lerp(to.GroundHorizon, t),
				from.Ambient.Lerp(to.Ambient, t),
				Mathf.Lerp(from.AmbientEnergy, to.AmbientEnergy, t),
				from.Sun.Lerp(to.Sun, t),
				Mathf.Lerp(from.SunEnergy, to.SunEnergy, t),
				Mathf.Lerp(from.MoonEnergy, to.MoonEnergy, t));
		}

		private LightKey(
			float hour,
			Color skyTop,
			Color skyHorizon,
			Color groundBottom,
			Color groundHorizon,
			Color ambient,
			float ambientEnergy,
			Color sun,
			float sunEnergy,
			float moonEnergy)
		{
			Hour = hour;
			SkyTop = skyTop;
			SkyHorizon = skyHorizon;
			GroundBottom = groundBottom;
			GroundHorizon = groundHorizon;
			Ambient = ambient;
			AmbientEnergy = ambientEnergy;
			Sun = sun;
			SunEnergy = sunEnergy;
			MoonEnergy = moonEnergy;
		}
	}
}

public readonly struct DayLightSample
{
	public DayLightSample(
		float sunElevation,
		float daylight,
		Color skyTop,
		Color skyHorizon,
		Color groundBottom,
		Color groundHorizon,
		Color ambient,
		float ambientEnergy,
		Color sunColor,
		float sunEnergy,
		float moonEnergy,
		float sunPitch,
		float sunYaw)
	{
		SunElevation = sunElevation;
		Daylight = daylight;
		SkyTop = skyTop;
		SkyHorizon = skyHorizon;
		GroundBottom = groundBottom;
		GroundHorizon = groundHorizon;
		Ambient = ambient;
		AmbientEnergy = ambientEnergy;
		SunColor = sunColor;
		SunEnergy = sunEnergy;
		MoonEnergy = moonEnergy;
		SunPitch = sunPitch;
		SunYaw = sunYaw;
	}

	public float SunElevation { get; }
	public float Daylight { get; }
	public Color SkyTop { get; }
	public Color SkyHorizon { get; }
	public Color GroundBottom { get; }
	public Color GroundHorizon { get; }
	public Color Ambient { get; }
	public float AmbientEnergy { get; }
	public Color SunColor { get; }
	public float SunEnergy { get; }
	public float MoonEnergy { get; }
	public float SunPitch { get; }
	public float SunYaw { get; }
}
