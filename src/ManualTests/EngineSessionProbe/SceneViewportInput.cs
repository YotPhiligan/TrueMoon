using TrueMoon.Aether;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Argentis;

namespace EngineSessionProbe;

// Pure bounded coalescing state. UI key repeat never creates another E press; quick down/up is preserved.
internal sealed class SceneInputBuffer
{
    private GameButtons _held;
    private bool _interactDown, _press;
    private float _yaw, _pitch;
    internal void Key(UiKey key, bool down)
    {
        if (key == UiKey.E) { _press |= down && !_interactDown; _interactDown = down; return; }
        var button = key switch { UiKey.W => GameButtons.Forward, UiKey.S => GameButtons.Backward, UiKey.A => GameButtons.Left, UiKey.D => GameButtons.Right, _ => GameButtons.None };
        if (down) _held |= button; else _held &= ~button;
    }
    internal void Look(float dx, float dy)
    {
        if (!float.IsFinite(dx) || !float.IsFinite(dy)) return;
        _yaw = Math.Clamp(_yaw - dx * .0025f, -.5f, .5f); _pitch = Math.Clamp(_pitch - dy * .0025f, -.5f, .5f);
    }
    internal GameInputState Take()
    {
        var result = new GameInputState(true, _held, _yaw, _pitch, _press);
        _yaw = _pitch = 0; _press = false; return result;
    }
    internal void Clear() { _held = GameButtons.None; _yaw = _pitch = 0; _interactDown = _press = false; }
}

// App composition owns RunAsync and drains it before Aether/window teardown. No task is created by a UI event.
internal sealed class SceneViewportInput(AetherHost host, HostedUiWindow window, ViewportView view)
{
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _send = new(1, 1);
    private readonly SceneInputBuffer _buffer = new();
    private Capture? _capture;
    private SessionIdentity? _release;
    private IInputContext? _context;
    private float _x, _y;
    private ulong _epoch;
    private int _sent;
    internal int SentPackets => Volatile.Read(ref _sent);
    internal bool Captured { get { lock (_gate) return _capture != null; } }
    internal Exception? Error { get; private set; }

    internal bool Handle(UiInput input, IInputContext context)
    {
        if (input.Kind == InputKind.PointerDown && input.Button == 0 && !Captured)
        {
            var status = host.Supervisor.Status; var presentation = host.Frames.Presentation;
            if (status.State is not (SessionState.Paused or SessionState.Playing) || presentation == null || !view.Canvas.ImageBounds.Contains(input.X, input.Y)) return false;
            context.Focus(view.Canvas); context.Capture(view.Canvas); _context = context; _x = input.X; _y = input.Y;
            lock (_gate) { _buffer.Clear(); _capture = new(status.Identity!, presentation, status.State, ++_epoch); }
            return true;
        }
        if (!Captured) return false;
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Escape) { Cancel(returnFocus: true); return true; }
        lock (_gate)
        {
            if (input.Kind is InputKind.KeyDown or InputKind.KeyUp) _buffer.Key(input.Key, input.Kind == InputKind.KeyDown);
            if (input.Kind == InputKind.PointerMove)
            { _buffer.Look(input.X - _x, input.Y - _y); _x = input.X; _y = input.Y; }
        }
        return true;
    }

    // UI owner only: cancelling focus/capture is idempotent and clears state before any async release.
    internal void Cancel(bool returnFocus = false)
    {
        lock (_gate) { if (_capture != null) _release = _capture.Identity; _capture = null; _buffer.Clear(); ++_epoch; }
        var context = _context; _context = null;
        if (context != null)
        {
            context.ReleaseCapture(view.Canvas);
            if (returnFocus && !view.PauseButton.IsDisposed && view.PauseButton.IsAttached) context.Focus(view.PauseButton);
        }
    }

    internal async Task<ControlReply?> FlushAsync(CancellationToken token = default)
    {
        await _send.WaitAsync(token).ConfigureAwait(false);
        try
        {
            SessionIdentity? identity; GameInputState state; ulong resetEpoch = 0, sendingEpoch;
            lock (_gate)
            {
                var status = host.Supervisor.Status;
                if (_capture is { } capture && (status.Identity != capture.Identity || status.State != capture.Mode ||
                    !ReferenceEquals(host.Frames.Presentation, capture.Presentation) ||
                    host.Editor.Status.Operation is GameEditorOperation.Pause or GameEditorOperation.Replace or GameEditorOperation.Stop))
                { _release = capture.Identity; _capture = null; _buffer.Clear(); resetEpoch = ++_epoch; }
                if (_release != null) { identity = _release; state = default; _release = null; }
                else { identity = _capture?.Identity; state = identity == null ? default : _buffer.Take(); }
                sendingEpoch = _epoch;
            }
            if (resetEpoch != 0) PostCancel(resetEpoch);
            if (identity == null) return null;
            // Do not create a command when its captured runtime has already changed.
            if (host.Supervisor.Status.Identity != identity || host.Supervisor.Status.State is not (SessionState.Paused or SessionState.Playing)) return null;
            try
            {
                var reply = await host.Supervisor.SendInputAsync(identity, state, token).ConfigureAwait(false); Interlocked.Increment(ref _sent);
                if (reply.InputOutcome == (int)GameInputOutcome.Unsupported)
                { Error = new NotSupportedException("Game does not support input."); ResetFromSender(sendingEpoch); }
                return reply;
            }
            catch (Exception error) when (error is OperationCanceledException or InvalidOperationException && !token.IsCancellationRequested)
            { ResetFromSender(sendingEpoch); return null; } // Stop/replacement admission never redirects this packet.
        }
        finally { _send.Release(); }
    }

    internal async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1d / 30));
        try { while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false)) await FlushAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Error = error; ResetFromSender(); }
    }
    private void ResetFromSender(ulong? expectedEpoch = null)
    {
        ulong epoch;
        lock (_gate) { if (expectedEpoch != null && expectedEpoch != _epoch) return; _capture = null; _release = null; _buffer.Clear(); epoch = ++_epoch; }
        PostCancel(epoch);
    }
    private void PostCancel(ulong epoch)
    {
        try { window.PostWindow(_ => { lock (_gate) { if (_epoch != epoch) return; } Cancel(returnFocus: true); }); }
        catch (ObjectDisposedException) { /* App has already drained/stopped the UI owner. */ }
    }
    private sealed record Capture(SessionIdentity Identity, ViewportPresentation Presentation, SessionState Mode, ulong Epoch);
}
