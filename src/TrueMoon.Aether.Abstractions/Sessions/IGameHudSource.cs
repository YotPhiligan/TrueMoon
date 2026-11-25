namespace TrueMoon.Aether.Sessions;

/// <summary>Optional text HUD supplied by the active game session.</summary>
public interface IGameHudSource
{
    /// <summary>Reads the immutable HUD after loading or updating the game, on the session's serialized path.</summary>
    /// <returns>The current HUD, or null for a scene without one.</returns>
    GameHudSnapshot? ReadHud();
}
