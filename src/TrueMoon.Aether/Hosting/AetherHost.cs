using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Gpu;

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
        Frames = new(Supervisor, options.ViewportTransport?.Invoke(Gpu) ?? (options.ViewportTransport == null ? null : throw new InvalidOperationException("Viewport factory returned null.")));
        Builds = new(options.HostContractId, options.BuildDirectory!, options.BuildTimeout);
        Replacement = new(Supervisor, Frames, options.ReplacementDeadlines);
        Editor = new(Supervisor, Frames, Builds, Replacement, options.HostContractId);
    }

    /// <summary>Gets the borrowed App-owned session controller. Start is an explicit operation.</summary>
    public SessionSupervisor Supervisor { get; }
    /// <summary>Gets the borrowed App-owned frame/presentation lifecycle; CPU is the default transport.</summary>
    public FrameSession Frames { get; }
    /// <summary>Gets the borrowed App-owned compiler. Completion returns a candidate without applying it.</summary>
    public GameBuildCompiler Builds { get; }
    /// <summary>Gets the borrowed transaction controller for explicit selected-build replacement and one rollback.</summary>
    public GameReplacement Replacement { get; }
    /// <summary>Gets the borrowed App-owned Editor command and selected-build controller.</summary>
    public GameEditor Editor { get; }
    /// <summary>Gets the borrowed App-owned lazy GPU resource owner; ordinary sessions remain on CPU frames.</summary>
    public GpuResourceHost Gpu { get; } = new();

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
            Builds.CloseAdmission();
            Editor.CloseAdmission();
            Gpu.CloseAdmission();
        }
        _ = DisposeOwnedAsync(completion);
        return new(completion.Task);
    }

    private async Task DisposeOwnedAsync(TaskCompletionSource completion)
    {
        List<Exception> errors = [];
        var buildCleanup = Builds.DisposeAsync().AsTask();
        try { await Gpu.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        try { await Editor.StopAsync().ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        try { await Supervisor.Admission.DrainAsync().ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        try { await Frames.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        try { await Supervisor.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        try { await buildCleanup.ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) completion.TrySetResult();
        else completion.TrySetException(errors.Count == 1 ? errors[0] : new AggregateException("Aether cleanup failed.", errors));
    }
}
