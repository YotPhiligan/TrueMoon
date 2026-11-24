using TrueMoon.Aether.Abstractions.Input;
using TrueMoon.Aether.Runtime;

namespace TrueMoon.Aether.Player.Scenarios;

// The window is borrowed. Only the original Player/App registration owns it.
internal sealed class WindowScenarioDriver(PlayerWindow window) : IPlayerScenarioDriver
{
    public bool SupportsFrameCapture => true;

    public Task<PlayerScenarioStep> AdvanceScenarioAsync(GameInputState input, bool capture, bool observe, CancellationToken cancellationToken)
        => window.AdvanceScenarioAsync(input, capture, observe, cancellationToken);
}

internal sealed class HeadlessScenarioDriver(PlayerSession session) : IPlayerScenarioDriver
{
    private int _advancing;

    public bool SupportsFrameCapture => false;

    public async Task<PlayerScenarioStep> AdvanceScenarioAsync(GameInputState input, bool capture, bool observe, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _advancing, 1, 0) != 0)
            throw new InvalidOperationException("A headless scenario tick is already in progress.");
        try
        {
            if (session.Status.State != PlayerState.Paused || session.Surface != null)
                throw new InvalidOperationException("A headless scenario requires an active paused Player session without a surface.");
            // An explicit paused step uses the same game tick and leaves the mesh presentation fence intact.
            return await PlayerScenarioTick.AdvanceAsync(session, input, observe, paused: true, cancellationToken).ConfigureAwait(false);
        }
        finally { Volatile.Write(ref _advancing, 0); }
    }
}

internal static class PlayerScenarioTick
{
    internal static async Task<PlayerScenarioStep> AdvanceAsync(PlayerSession session, GameInputState input, bool observe,
        bool paused, CancellationToken token)
    {
        var expectedTick = checked(session.Status.Tick + 1);
        var outcome = await session.InputAsync(input, token).ConfigureAwait(false);
        var status = paused
            ? await session.StepAsync(token).ConfigureAwait(false)
            : await session.FixedTickAsync(token).ConfigureAwait(false);
        if (status.Tick != expectedTick)
            throw new InvalidOperationException($"Expected Player tick {expectedTick}, observed {status.Tick} ({status.State}).");
        var observation = observe ? await session.ReadScenarioObservationAsync(status.Tick, token).ConfigureAwait(false) : null;
        return new(status, outcome, null) { Observation = observation };
    }
}
