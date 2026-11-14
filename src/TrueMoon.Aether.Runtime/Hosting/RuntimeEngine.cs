using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Sessions;

namespace TrueMoon.Aether.Runtime.Hosting;

/// <summary>App-owned runtime resources, independent of compiler, projects and Editor UI.</summary>
public sealed class RuntimeEngine : IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private Task? _stop;
    internal RuntimeEngine(SessionSupervisor supervisor, FrameSession frames, GameReplacement replacement, GpuResourceHost gpu)
    { Supervisor = supervisor; Frames = frames; Replacement = replacement; Gpu = gpu; }
    public SessionSupervisor Supervisor { get; }
    public FrameSession Frames { get; }
    public GameReplacement Replacement { get; }
    public GpuResourceHost Gpu { get; }
    internal void EnsureOpen() { lock (_gate) ObjectDisposedException.ThrowIf(_stop != null, this); }
    internal void CloseAdmission() { Supervisor.CloseAdmission(); Gpu.CloseAdmission(); }
    /// <summary>Closes admission and drains owned resources; failed cleanup remains retryable.</summary>
    public Task StopAsync()
    {
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_stop is { IsCompletedSuccessfully: true } or { IsCompleted: false }) return _stop;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _stop = completion.Task;
            CloseAdmission();
        }
        _ = CleanupAsync(completion); return completion.Task;
    }
    private async Task CleanupAsync(TaskCompletionSource completion)
    {
        List<Exception> errors = [];
        try { await Gpu.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
        try { await Supervisor.Admission.DrainAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
        try { await Frames.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
        try { await Supervisor.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) completion.TrySetResult();
        else completion.TrySetException(errors.Count == 1 ? errors[0] : new AggregateException("Runtime cleanup failed.", errors));
    }
    public void Dispose() => StopAsync().GetAwaiter().GetResult();
    public ValueTask DisposeAsync() => new(StopAsync());
}
