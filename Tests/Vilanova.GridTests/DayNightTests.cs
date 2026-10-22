using Xunit;

namespace Vilanova.GridTests;

public class DayNightTests
{
	[Fact]
	public void TwelveGameHoursLastTwelveRealMinutes()
	{
		var hour = DayNightCycle.Advance(8.0f, 12.0f * 60.0f);
		Assert.Equal(20.0f, hour, 2);
	}

	[Fact]
	public void OneGameHourLastsOneRealMinute()
	{
		Assert.Equal(11.0f, DayNightCycle.Advance(10.0f, 60.0f), 2);
	}

	[Fact]
	public void ClockWrapsAtMidnight()
	{
		Assert.Equal(1.0f, DayNightCycle.Advance(23.0f, 120.0f), 2);
	}

	[Fact]
	public void NoonIsDayAndMidnightIsNight()
	{
		var noon = DayNightCycle.Sample(12.0f);
		var midnight = DayNightCycle.Sample(0.0f);
		Assert.True(noon.SunElevation > 0.9f);
		Assert.True(noon.SunEnergy > 0.9f);
		Assert.Equal(0.0f, noon.MoonEnergy, 2);
		Assert.True(midnight.SunElevation < -0.9f);
		Assert.Equal(0.0f, midnight.SunEnergy, 2);
		Assert.True(midnight.MoonEnergy > 0.2f);
		Assert.True(noon.SunPitch < 0.0f);
		Assert.True(midnight.SunPitch > 0.0f);
	}
}
