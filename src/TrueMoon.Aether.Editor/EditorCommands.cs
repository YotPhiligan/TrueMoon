using TrueMoon.Aether.Editing;

namespace TrueMoon.Aether.Editor;

/// <summary>Tracks every asynchronous UI command; Stop/Cancel interrupt accepted work and close drains it.</summary>
public sealed class EditorCommands
{
    private readonly Func<EditorCommand, CancellationToken, Task> _dispatch;
    private readonly CancellationToken _lifetime;
    private readonly Lock _gate = new();
    private Task _pending = Task.CompletedTask;
    private Exception? _error;
    private bool _closing;
    public EditorCommands(GameEditor editor, Task ready, CancellationToken lifetime) : this(Dispatch(editor, ready), lifetime) { }
    public EditorCommands(Func<EditorCommand, CancellationToken, Task> dispatch, CancellationToken lifetime)
    { ArgumentNullException.ThrowIfNull(dispatch); _dispatch = dispatch; _lifetime = lifetime; }
    public Task Pending { get { lock (_gate) return _pending; } }
    public Exception? Error { get { lock (_gate) return _error; } }
    public void Execute(EditorCommand command)
    {
        if (!Enum.IsDefined(command)) throw new ArgumentOutOfRangeException(nameof(command));
        lock (_gate)
        {
            if (_closing) return;
            if (command is EditorCommand.Stop or EditorCommand.Cancel)
            {
                var pending = _pending;
                _pending = Run(async () => { await _dispatch(command, CancellationToken.None); await pending; }); return;
            }
            if (!_pending.IsCompleted) return;
            _error = null;
            _pending = Run(() => _dispatch(command, _lifetime));
        }
    }
    private Task Run(Func<Task> action) => Task.Run(async () => { try { await action(); } catch (OperationCanceledException) { } catch (Exception error) { lock (_gate) _error = error; } });
    public async Task StopAsync()
    {
        Task pending; lock (_gate) { _closing = true; pending = _pending; }
        try { await _dispatch(EditorCommand.Stop, CancellationToken.None).ConfigureAwait(false); await pending.ConfigureAwait(false); }
        catch { lock (_gate) _closing = false; throw; }
    }
    private static Func<EditorCommand, CancellationToken, Task> Dispatch(GameEditor editor, Task ready)
    {
        ArgumentNullException.ThrowIfNull(editor); ArgumentNullException.ThrowIfNull(ready);
        return async (command, token) =>
        {
            switch (command)
            {
                case EditorCommand.Build: await editor.BuildAsync(token); break;
                case EditorCommand.Start: await ready.WaitAsync(token); await editor.StartAsync(960, 540, token); break;
                case EditorCommand.Play: await editor.PlayAsync(token); break;
                case EditorCommand.Pause: await editor.PauseAsync(token); break;
                case EditorCommand.Step: await editor.StepAsync(token); break;
                case EditorCommand.Replace: await editor.ReplaceAsync(token); break;
                case EditorCommand.Stop: await editor.StopAsync(); break;
                case EditorCommand.Cancel: await editor.CancelAsync(); break;
                default: throw new ArgumentOutOfRangeException(nameof(command));
            }
        };
    }
}
