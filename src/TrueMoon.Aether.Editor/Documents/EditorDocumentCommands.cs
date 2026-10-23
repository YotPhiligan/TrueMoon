using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Editor.Documents;

/// <summary>Serializes document actions and user decisions, without holding controls or owning project sessions.</summary>
/// <remarks>The App owns this service. Window close calls RequestExitAsync; terminal shutdown calls StopAsync.
/// Snapshot refresh belongs to the UI owner. Recoverable errors veto close and remain available through Read.</remarks>
public sealed class EditorDocumentCommands : IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly EditorProjects _projects;
    private readonly Func<bool> _canTransition;
    private readonly Func<Task> _stopRuntime;
    private readonly CancellationToken _lifetime;
    private Task _pending = Task.CompletedTask;
    private Task<bool>? _exit;
    private CancellationTokenSource? _cancellation;
    private TaskCompletionSource<(DocumentCloseChoice Choice, string? Path)>? _decision;
    private DocumentCommand _operation;
    private Exception? _error;
    private Exception? _cancellationError;
    private bool _busy, _exiting;
    private volatile bool _closed;

    /// <summary>Creates an initially idle coordinator borrowing project and runtime boundaries.</summary>
    /// <param name="projects">App-owned active project service.</param>
    /// <param name="canTransition">True only while runtime/compiler command work is stopped and idle.</param>
    /// <param name="stopRuntime">Closes runtime command admission and drains work before successful exit.</param>
    /// <param name="lifetime">Application cancellation.</param>
    public EditorDocumentCommands(EditorProjects projects, Func<bool> canTransition, Func<Task> stopRuntime,
        CancellationToken lifetime = default)
    {
        ArgumentNullException.ThrowIfNull(projects); ArgumentNullException.ThrowIfNull(canTransition); ArgumentNullException.ThrowIfNull(stopRuntime);
        _projects = projects; _canTransition = canTransition; _stopRuntime = stopRuntime; _lifetime = lifetime;
    }

    /// <summary>Gets actual completion of the last accepted UI action, including a pending decision.</summary>
    public Task Pending { get { lock (_gate) return _exiting ? _exit! : _pending; } }

    /// <summary>Reads current operation, document and error state without raising UI notifications.</summary>
    /// <returns>An immutable observation; previously returned document snapshots remain valid.</returns>
    public EditorDocumentCommandState Read()
    {
        lock (_gate)
        {
            var project = _projects.Active;
            return new(_operation, _busy, _decision != null, _exiting, _closed, project?.Documents?.Read(),
                project?.Project.ScenesDirectory, _error, !_closed && _canTransition());
        }
    }

    /// <summary>Checks the same admission rules used by Execute.</summary>
    /// <param name="command">Requested action.</param><returns>Whether it can start now.</returns>
    public bool CanExecute(DocumentCommand command)
    {
        Validate(command);
        lock (_gate) return Available(command);
    }

    private bool Available(DocumentCommand command)
    {
        if (_closed || _busy || _exiting || _lifetime.IsCancellationRequested || !_canTransition()) return false;
        var document = _projects.Active?.Documents?.Read();
        if (document == null || document.IsClosed || document.Operation != DocumentOperation.None) return false;
        return command is DocumentCommand.New or DocumentCommand.Open ||
            document.Content != null && command is DocumentCommand.Save or DocumentCommand.SaveAs or DocumentCommand.Close;
    }

    /// <summary>Starts one action; unavailable/concurrent actions leave state untouched.</summary>
    /// <param name="command">New/Open/Save/SaveAs/Close.</param><param name="path">Scenes-relative Open/SaveAs or untitled Save target.</param>
    /// <returns>True when accepted. Observe Pending and Read for completion/errors.</returns>
    public bool Execute(DocumentCommand command, string path = "")
    {
        Validate(command); ArgumentNullException.ThrowIfNull(path);
        TaskCompletionSource completion; CancellationTokenSource cancellation; SceneDocumentSession document;
        lock (_gate)
        {
            if (!Available(command)) return false;
            document = _projects.Active!.Documents!;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _cancellation = cancellation; _pending = completion.Task; _busy = true; _operation = command; _error = null; _cancellationError = null;
        }
        _ = Task.Run(async () =>
        {
            Exception? failure = null;
            try
            {
                var token = cancellation.Token; token.ThrowIfCancellationRequested();
                var choice = DocumentCloseChoice.Cancel; string? saveAs = null;
                if (command is DocumentCommand.New or DocumentCommand.Open or DocumentCommand.Close && document.Read().IsDirty)
                    (choice, saveAs) = await DecideAsync(token).ConfigureAwait(false);
                if (choice == DocumentCloseChoice.Cancel && document.Read().IsDirty &&
                    command is DocumentCommand.New or DocumentCommand.Open or DocumentCommand.Close) return;
                switch (command)
                {
                    case DocumentCommand.New:
                        await document.NewAsync(new(new(Guid.NewGuid()), 0, [], [], []), choice, saveAs, token).ConfigureAwait(false); break;
                    case DocumentCommand.Open: await document.LoadAsync(path, choice, saveAs, token).ConfigureAwait(false); break;
                    case DocumentCommand.Save:
                        if (document.Read().Path == null) await document.SaveAsAsync(path, token).ConfigureAwait(false);
                        else await document.SaveAsync(token).ConfigureAwait(false);
                        break;
                    case DocumentCommand.SaveAs: await document.SaveAsAsync(path, token).ConfigureAwait(false); break;
                    case DocumentCommand.Close: await document.CloseAsync(choice, saveAs, token).ConfigureAwait(false); break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { failure = error; }
            finally
            {
                lock (_gate)
                {
                    _decision = null; _cancellation = null; cancellation.Dispose(); _error = Combine(failure, _cancellationError); _cancellationError = null;
                    if (!_exiting) { _busy = false; _operation = DocumentCommand.None; }
                }
                completion.TrySetResult();
            }
        });
        return true;
    }

    private async Task<(DocumentCloseChoice Choice, string? Path)> DecideAsync(CancellationToken token)
    {
        TaskCompletionSource<(DocumentCloseChoice, string?)> decision;
        lock (_gate)
        {
            token.ThrowIfCancellationRequested();
            if (_closed) return (DocumentCloseChoice.Discard, null);
            decision = new(TaskCreationOptions.RunContinuationsAsynchronously); _decision = decision;
        }
        try { return await decision.Task.WaitAsync(token).ConfigureAwait(false); }
        finally { lock (_gate) if (ReferenceEquals(_decision, decision)) _decision = null; }
    }

    /// <summary>Resolves the current decision once. Save uses saveAsPath only for an untitled document.</summary>
    /// <param name="choice">Save/Discard/Cancel.</param><param name="saveAsPath">Explicit target for untitled content.</param>
    /// <returns>True when a pending decision accepted the choice.</returns>
    public bool Choose(DocumentCloseChoice choice, string? saveAsPath = null)
    {
        if (!Enum.IsDefined(choice)) throw new ArgumentOutOfRangeException(nameof(choice));
        lock (_gate)
        {
            if (_decision == null) return false;
            var accepted = _decision.TrySetResult((choice, _projects.Active?.Documents?.Read().Path == null ? saveAsPath : null));
            if (accepted) _decision = null;
            return accepted;
        }
    }

    /// <summary>Cancels accepted work or resolves a decision as Cancel; actual IO still drains.</summary>
    public void Cancel()
    {
        lock (_gate)
        {
            _decision?.TrySetResult((DocumentCloseChoice.Cancel, null));
            _cancellationError = Combine(_cancellationError, CancelWork());
            _error = Combine(_error, _cancellationError);
        }
    }

    /// <summary>Requests a user-vetoable exit. Concurrent requests share completion; failed Save leaves the window usable.</summary>
    /// <returns>True only after document close and runtime drain; false for cancel or a recoverable failure.</returns>
    public Task<bool> RequestExitAsync() => ExitAsync(force: false);

    /// <summary>Terminal shutdown, cancelling decisions and draining IO before forced discard and runtime stop.</summary>
    /// <returns>Actual shared cleanup completion. Failures retain owners for explicit retry.</returns>
    public async Task StopAsync() { await ExitAsync(force: true).ConfigureAwait(false); }

    private Task<bool> ExitAsync(bool force)
    {
        Task work; TaskCompletionSource<bool> completion; Exception? cancellationFailure;
        lock (_gate)
        {
            if (_exit is { IsCompleted: false })
            {
                if (force)
                {
                    _closed = true; _decision?.TrySetResult((DocumentCloseChoice.Discard, null));
                    _cancellationError = Combine(_cancellationError, CancelWork());
                }
                return _exit;
            }
            if (_closed && !force) return Task.FromResult(true);
            if (_closed && _exit?.IsCompletedSuccessfully == true && _exit.Result) return _exit;
            if (force) _closed = true;
            _exiting = true; _busy = true; _error = null; work = _pending; _operation = DocumentCommand.Exit;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _exit = completion.Task;
            _decision?.TrySetResult((DocumentCloseChoice.Cancel, null)); cancellationFailure = CancelWork();
        }
        _ = Task.Run(async () =>
        {
            var success = false; Exception? failure = null;
            try
            {
                await work.ConfigureAwait(false);
                if (cancellationFailure != null) throw cancellationFailure;
                var document = _projects.Active?.Documents;
                var choice = DocumentCloseChoice.Discard; string? path = null;
                if (!_closed && document?.Read().IsDirty == true)
                    (choice, path) = await DecideAsync(CancellationToken.None).ConfigureAwait(false);
                if (_closed) choice = DocumentCloseChoice.Discard;
                if (choice == DocumentCloseChoice.Cancel) return;
                if (document != null && !await document.CloseAsync(choice, path).ConfigureAwait(false)) return;
                await _stopRuntime().ConfigureAwait(false); success = true;
            }
            catch (Exception error) { failure = error; }
            finally
            {
                lock (_gate)
                {
                    _decision = null; _exiting = false; _busy = false; _operation = DocumentCommand.None;
                    _error = failure; if (success) _closed = true;
                }
                if (_closed && failure != null) completion.TrySetException(failure); else completion.TrySetResult(success);
            }
        });
        return completion.Task;
    }

    private static void Validate(DocumentCommand command)
    {
        if (!Enum.IsDefined(command) || command is DocumentCommand.None or DocumentCommand.Exit)
            throw new ArgumentOutOfRangeException(nameof(command));
    }

    private Exception? CancelWork()
    { try { _cancellation?.Cancel(); return null; } catch (Exception error) { return error; } }
    private static Exception? Combine(Exception? first, Exception? second) => first == null ? second :
        second == null || ReferenceEquals(first, second) ? first : new AggregateException(first, second);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(StopAsync());
    /// <inheritdoc />
    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
