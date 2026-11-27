using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Runtime;

namespace TrueMoon.Aether.Player.Scenarios;

/// <summary>Optional persistence for an absolute tick in a version 1 Player scenario.</summary>
/// <param name="Load">Loads a checkpoint for the frozen Player start and the uppercase SHA-256 of the exact scenario bytes, before startup.</param>
/// <param name="SaveAtTick">Completed absolute tick to save, strictly after the resume tick when resuming.</param>
/// <param name="Save">Persists the paused checkpoint for the frozen Player start and scenario SHA-256. Supply together with <paramref name="SaveAtTick"/>; throw if persistence fails.</param>
public sealed record PlayerScenarioCheckpointOptions(
    Func<PlayerStart, string, GameCheckpoint>? Load = null,
    int? SaveAtTick = null,
    Action<PlayerStart, string, GameCheckpoint>? Save = null);
