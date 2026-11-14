using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Optional standard Runtime + Editor UI window. Owns subscriptions/initialization; borrows UI, runtime and models.</summary>
public sealed class RuntimeEditorWindow : IAsyncDisposable
{
    private readonly HostedUiWindow _window;
    private readonly RuntimeEditorSession _editor;
    private readonly CancellationTokenSource _end = new();
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _initialization = Task.CompletedTask;
    private Task? _dispose;
    private bool _attached, _initializing;
    internal RuntimeEditorWindow(HostedUiWindow window, RuntimeEditorSession editor) { _window = window; _editor = editor; }
    /// <summary>First actual presentation and shared-viewport context initialization.</summary>
    public Task Ready => _ready.Task;
    internal Task StartAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_dispose != null, this);
        if (!_attached) { _window.FramePresented += Presented; _window.Closing += ClosingAsync; _attached = true; }
        return Task.CompletedTask;
    }
    private void Presented(UiSession ui)
    {
        _editor.Present(ui, (EditorView)ui.Root);
        lock (_gate)
        {
            if (_initializing || _dispose != null) return;
            _initializing = true; _initialization = InitializeAsync();
        }
    }
    private async Task InitializeAsync()
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_end.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await _editor.InitializeViewportAsync(_window, timeout.Token).ConfigureAwait(false); _ready.TrySetResult();
        }
        catch (Exception error) { _ready.TrySetException(error); }
    }
    internal async Task WaitReadyAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var completed = await Task.WhenAny(Ready, _window.Completion).WaitAsync(timeout.Token).ConfigureAwait(false);
        await completed.ConfigureAwait(false);
        if (!Ready.IsCompleted) throw new InvalidOperationException("Runtime editor window closed before first presentation.");
        await Ready.ConfigureAwait(false);
    }
    private async Task<bool> ClosingAsync(UiCloseReason reason) { await _editor.StopAsync().ConfigureAwait(false); return true; }
    internal async Task StopAsync()
    {
        // Surface/GL cleanup must finish before stopping the borrowed native window.
        await _editor.StopAsync().ConfigureAwait(false);
        try { await _window.StopAsync().ConfigureAwait(false); }
        finally { await DisposeAsync().ConfigureAwait(false); }
    }
    /// <summary>Detaches callbacks and drains context initialization. Does not dispose borrowed UI/runtime resources.</summary>
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_dispose != null) return new(_dispose);
            if (_attached) { _window.FramePresented -= Presented; _window.Closing -= ClosingAsync; _attached = false; }
            _end.Cancel(); _ready.TrySetCanceled(); _dispose = DrainAsync(); return new(_dispose);
        }
    }
    private async Task DrainAsync() { try { await _initialization.ConfigureAwait(false); } finally { _end.Dispose(); } }
}
