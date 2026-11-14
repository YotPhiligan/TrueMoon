using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Editing;
using TrueMoon.Aether.Tooling.Preview;

namespace TrueMoon.Aether.Tooling.Hosting;

/// <summary>Owns the engine session and frame resources for one TrueMoon App.</summary>
/// <remarks>Resolve this service after UseAether registration. App Stop permanently closes
/// this host; use session Stop for repeated Play/Stop cycles while the App remains active.</remarks>
public sealed class AetherHost : IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private Task? _dispose;
    private readonly RuntimeEngine _runtime;

    internal AetherHost(RuntimeEngine runtime, GameBuildCompiler builds, GameEditor editor, ScenePreview preview)
    {
        _runtime = runtime; Supervisor = runtime.Supervisor; Frames = runtime.Frames; Builds = builds;
        Replacement = runtime.Replacement; Editor = editor; Gpu = runtime.Gpu; Preview = preview;
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
    public GpuResourceHost Gpu { get; }
    /// <summary>Gets the borrowed App-owned static Edit preview controller; it never builds or loads Game.Logic.</summary>
    public ScenePreview Preview { get; }

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
            Preview.CloseAdmission();
            _runtime.CloseAdmission();
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
        try { await Preview.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        try { await Editor.StopAsync().ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        try { await _runtime.StopAsync().ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        try { await buildCleanup.ConfigureAwait(false); }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) completion.TrySetResult();
        else completion.TrySetException(errors.Count == 1 ? errors[0] : new AggregateException("Aether cleanup failed.", errors));
    }
}
