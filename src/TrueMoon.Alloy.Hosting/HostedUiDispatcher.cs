namespace TrueMoon.Alloy.Hosting;

/// <summary>Owns an application's hosted windows on the shared Windows owner thread.</summary>
/// <remarks>Windows, sessions and devices have application lifetime. The idle background
/// thread is reused for the process lifetime; shutdown never terminates global GLFW state.
/// Direct Silk hosts must not run concurrently on a different thread.</remarks>
public sealed class HostedUiDispatcher : IStoppable, IAsyncDisposable
{
    private static readonly UiOwnerLoop Owner = new();
    internal static HostedUiDispatcher Shared { get; } = new();
    private readonly object _gate = new();
    private readonly HashSet<HostedUiWindow> _windows = [];
    private Task? _shutdown;

    internal bool IsOwnerThread => Owner.IsOwnerThread;
    internal void Wake() => Owner.Wake();

    internal void Start(HostedUiWindow window, Action initialize, Func<bool> tick, Action cleanup,
        Func<bool> closing, TaskCompletionSource ready, TaskCompletionSource completion)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Hosted owner-loop windows currently require Windows.");
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_shutdown != null, this);
            _windows.Add(window);
            try
            {
                Owner.Schedule(initialize, tick, cleanup, closing, ready, completion,
                    () => { lock (_gate) _windows.Remove(window); }, window.WaitMilliseconds);
            }
            catch { _windows.Remove(window); throw; }
        }
    }

    /// <summary>Closes this application's windows and waits for owner-thread cleanup.</summary>
    /// <param name="cancellationToken">Cancels the wait; shutdown continues.</param>
    /// <returns>A task completing after all accepted windows finish.</returns>
    /// <remarks>Completed window faults remain observable through each window's Completion.</remarks>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_shutdown == null)
            {
                var windows = _windows.Where(window => !window.Completion.IsCompleted).ToArray();
                foreach (var window in windows) window.Close();
                _shutdown = Task.WhenAll(windows.Select(window => window.Completion));
            }
            if (IsOwnerThread && !_shutdown.IsCompleted)
                throw new InvalidOperationException("Await dispatcher shutdown outside the UI owner thread.");
            return _shutdown.WaitAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(StopAsync());
}
