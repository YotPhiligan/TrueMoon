using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Hosting;

/// <summary>Owns the engine session and frame resources for one TrueMoon App.</summary>
/// <remarks>Resolve this service after UseAether registration. App Stop permanently closes
/// this host; use session Stop for repeated Play/Stop cycles while the App remains active.</remarks>
public sealed class AetherHost : IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private Task? _dispose;

    internal AetherHost(IRuntimeFactory factory, AetherOptions options)
    {
        Supervisor = new(factory, options.HostContractId, options.GameBuildId,
            options.ReportDirectory, options.Deadlines, options.StatusChanged);
        Frames = new(Supervisor);
    }

    /// <summary>Gets the borrowed App-owned session controller. Start is an explicit operation.</summary>
    public SessionSupervisor Supervisor { get; }
    /// <summary>Gets the borrowed App-owned CPU frame session.</summary>
    public FrameSession Frames { get; }

    internal void EnsureOpen()
    {
        lock (_gate) ObjectDisposedException.ThrowIf(_dispose != null, this);
    }

    /// <summary>Closes admission, drains frames and stops/reaps the owned game synchronously.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>Closes admission, drains frames and stops/reaps the owned game.</summary>
    /// <returns>Shared completion for concurrent calls. A failed cleanup may be retried explicitly.</returns>
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_dispose is { IsCompletedSuccessfully: true } or { IsCompleted: false }) return new(_dispose);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _dispose = completion.Task;
            Supervisor.CloseAdmission();
        }
        _ = DisposeOwnedAsync(completion);
        return new(completion.Task);
    }

    private async Task DisposeOwnedAsync(TaskCompletionSource completion)
    {
        List<Exception> errors = [];
        try { await Frames.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        try { await Supervisor.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) completion.TrySetResult();
        else completion.TrySetException(errors.Count == 1 ? errors[0] : new AggregateException("Aether cleanup failed.", errors));
    }
}
