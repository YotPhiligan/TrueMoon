namespace TrueMoon.Aether.Ecs;

/// <summary>Ordered simulation phases, each followed by deferred command playback.</summary>
public enum EcsPhase
{
    /// <summary>Input admission.</summary>
    Input,
    /// <summary>Gameplay and AI.</summary>
    Gameplay,
    /// <summary>Movement and physics integration.</summary>
    Movement,
    /// <summary>Hierarchy/derived transforms.</summary>
    Transform,
    /// <summary>Observational extraction.</summary>
    Extraction
}
