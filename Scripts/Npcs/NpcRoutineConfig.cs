namespace Vilanova;

public sealed class NpcRoutineConfig
{
	public float MoveSpeed { get; set; } = 2.05f;
	public float ArrivalRadius { get; set; } = 0.48f;
	public float MinIdleSeconds { get; set; } = 0.7f;
	public float MaxIdleSeconds { get; set; } = 2.2f;
	public float MinVisitSeconds { get; set; } = 4.0f;
	public float MaxVisitSeconds { get; set; } = 9.0f;
	public float VisitChance { get; set; } = 0.55f;
	public float RotationSmoothness { get; set; } = 12.0f;
	public float ExitRetrySeconds { get; set; } = 6.0f;
}
