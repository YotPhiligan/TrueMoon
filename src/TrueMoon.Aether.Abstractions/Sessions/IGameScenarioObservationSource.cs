namespace TrueMoon.Aether.Abstractions.Sessions;

/// <summary>Optional game capability for inspecting completed fixed ticks in scenario runs.</summary>
public interface IGameScenarioObservationSource
{
    /// <summary>Returns an owned snapshot of the current completed tick, or null when it is unavailable.</summary>
    /// <remarks>The local Player serializes the call with simulation. Implementations read state without mutating the game.</remarks>
    GameScenarioSnapshot? ReadScenarioObservation();
}
