namespace Vilanova;

/// <summary>
/// 3D physics layers (project settings):
/// 1 = Terrain, 2 = Structure, 3 = Player, 4 = Npc.
/// Player mask includes residents so they block each other without changing WASD.
/// </summary>
public static class CollisionLayers
{
	public const uint Terrain = 1;
	public const uint Structure = 2;
	public const uint Player = 4;
	public const uint Npc = 8;

	public const uint PlayerMask = Terrain | Structure | Npc;
	public const uint NpcMask = Terrain | Structure | Player | Npc;
}
