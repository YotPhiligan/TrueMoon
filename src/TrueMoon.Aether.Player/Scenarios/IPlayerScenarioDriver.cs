using TrueMoon.Aether.Abstractions.Input;

namespace TrueMoon.Aether.Player.Scenarios;

/// <summary>Drives one explicitly selected Player scenario profile on an active App.</summary>
public interface IPlayerScenarioDriver
{
    /// <summary>Whether a completed checkpoint can include a presented window frame.</summary>
    bool SupportsFrameCapture { get; }

    /// <summary>Stages input, completes one tick and reads the requested evidence at that tick.</summary>
    /// <param name="input">Complete held state and one-tick changes.</param>
    /// <param name="capture">Request a presented frame if this profile supports capture.</param>
    /// <param name="observe">Request the optional game observation.</param>
    /// <param name="cancellationToken">Cancels admission and the caller's wait. Stop the App to abort the run.</param>
    /// <returns>One completed tick. The headless profile returns a null frame.</returns>
    Task<PlayerScenarioStep> AdvanceScenarioAsync(GameInputState input, bool capture, bool observe, CancellationToken cancellationToken);
}
