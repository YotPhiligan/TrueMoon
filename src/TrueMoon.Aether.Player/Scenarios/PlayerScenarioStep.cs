using System.Collections.Immutable;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Player.Scenarios;

/// <summary>Result of one explicitly driven Player fixed tick.</summary>
/// <param name="Status">Observation taken immediately after the tick.</param>
/// <param name="Input">Outcome from the Player input protocol.</param>
/// <param name="Frame">Presented window composition when requested.</param>
public sealed record PlayerScenarioStep(PlayerStatus Status, GameInputOutcome Input, PlayerScenarioFrame? Frame)
{
    /// <summary>The requested game observation at this tick, when supplied by the game.</summary>
    public GameScenarioSnapshot? Observation { get; init; }
}

/// <summary>Detached PNG and identity of the frame presented in the Player window (scene and HUD).</summary>
/// <param name="Tick">Tick attached to the rendered frame.</param>
/// <param name="FrameId">Frame identifier within the surface generation.</param>
/// <param name="SurfaceGeneration">Generation admitted by the local Player.</param>
/// <param name="Width">PNG width in physical pixels.</param>
/// <param name="Height">PNG height in physical pixels.</param>
/// <param name="Scale">Logical-to-physical scale when the window presented.</param>
/// <param name="Png">Owned encoded image.</param>
public sealed record PlayerScenarioFrame(ulong Tick, ulong FrameId, ulong SurfaceGeneration,
    int Width, int Height, float Scale, ImmutableArray<byte> Png);
