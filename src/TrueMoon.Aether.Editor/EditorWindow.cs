using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Standard window adapter. Borrows the hosted window, editor services and UI; owns only its event subscriptions.</summary>
public sealed class EditorWindow : IDisposable, IAsyncDisposable
{
    private readonly HostedUiWindow _window;
    private readonly EditorSession _editor;
    private readonly EditorWorkspace _workspace;
    private readonly EditorViewportBinding _viewport;
    private readonly AetherHost _engine;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private Task _initialization = Task.CompletedTask;
    private Task? _dispose;
    private bool _attached, _initializing;

    internal EditorWindow(HostedUiWindow window, EditorSession editor, EditorWorkspace workspace,
        EditorViewportBinding viewport, AetherHost engine)
    { _window = window; _editor = editor; _workspace = workspace; _viewport = viewport; _engine = engine; }

    /// <summary>Completes after first presentation and, for a selected project, initialization of its GPU viewport.</summary>
    public Task Ready => _ready.Task;

    internal Task StartAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_dispose != null, this);
        if (!_attached)
        { _window.FramePresented += Presented; _window.Closing += Closing; _attached = true; }
        return Task.CompletedTask;
    }

    internal async Task WaitReadyAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var completed = await Task.WhenAny(Ready, _window.Completion).WaitAsync(timeout.Token).ConfigureAwait(false);
        await completed.ConfigureAwait(false);
        if (ReferenceEquals(completed, _window.Completion) && !Ready.IsCompleted)
            throw new InvalidOperationException("Editor window closed before first presentation.");
        await Ready.ConfigureAwait(false);
    }

    internal async Task StopAsync()
    {
        try { await _window.StopAsync().ConfigureAwait(false); }
        finally { await DisposeAsync().ConfigureAwait(false); }
    }

    private void Presented(UiSession ui)
    {
        var view = (EditorView)((WindowFrame)ui.Root).Content;
        _editor.Present(ui, view);
        lock (_gate)
        {
            if (_initializing || _dispose != null) return;
            _initializing = true;
            _initialization = InitializeAsync();
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (_workspace.HasProject)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                await _viewport.InitializeAsync(_window, _engine, timeout.Token).ConfigureAwait(false);
            }
            _ready.TrySetResult();
        }
        catch (Exception error) { _ready.TrySetException(error); }
    }

    private async Task<bool> Closing(UiCloseReason reason)
    {
        if (reason == UiCloseReason.Shutdown) { await _editor.StopAsync().ConfigureAwait(false); return true; }
        return await _editor.RequestCloseAsync().ConfigureAwait(false);
    }

    /// <summary>Detaches callbacks without disposing the borrowed window, view, model or engine.</summary>
    public void Dispose()
        => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>Detaches callbacks and cancels/drains any pending viewport initialization.</summary>
    /// <returns>Shared actual completion; borrowed UI and engine services are retained by their owners.</returns>
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_dispose != null) return new(_dispose);
            if (_attached) { _window.FramePresented -= Presented; _window.Closing -= Closing; _attached = false; }
            _lifetime.Cancel(); _viewport.Forget(); _ready.TrySetCanceled();
            _dispose = DrainAsync();
            return new(_dispose);
        }
    }

    private async Task DrainAsync()
    { try { await _initialization.ConfigureAwait(false); } finally { _lifetime.Dispose(); } }
}
