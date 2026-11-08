using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy.Runtime;

namespace TrueMoon.Aether.Editor;

/// <summary>App-local editor coordination borrowing engine, project and presentation services.</summary>
/// <remarks>The UI parent owns each factory-created view. Present on its UI owner thread.
/// Stop drains document/preview/runtime commands; DI remains the sole owner of their services.</remarks>
public sealed class EditorSession : IStartable, IStoppable
{
    private readonly AetherEditorOptions _options;
    private readonly AetherHost _engine;
    private readonly EditorProjects _projects;
    private readonly EditorWorkspace _workspace;
    private readonly EditorCommands _commands;
    private readonly EditorDocumentCommands _documents;
    private readonly EditorViewModel _model;
    private readonly EditorViewportBinding _viewport;
    private readonly EditorDiagnosticsViewModel? _diagnostics;
    private readonly IFactory<EditorViewportController,EditorViewportArguments>? _controllers;
    private long _nextDiagnosticsRefresh;

    internal EditorSession(AetherEditorOptions options, AetherHost engine, EditorProjects projects,
        EditorWorkspace workspace, EditorCommands commands, EditorDocumentCommands documents,
        EditorViewModel model, EditorViewportBinding viewport, EditorDiagnosticsViewModel? diagnostics = null,
        IFactory<EditorViewportController,EditorViewportArguments>? controllers = null)
    {
        _options = options; _engine = engine; _projects = projects; _workspace = workspace;
        _commands = commands; _documents = documents; _model = model; _viewport = viewport;
        _diagnostics = diagnostics;
        _controllers = controllers;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_options.ProjectPath == null) return;
        try
        {
            if (!_documents.Execute(DocumentCommand.OpenProject, _options.ProjectPath))
                throw new InvalidOperationException("Project startup was not admitted.");
            using var cancellation = cancellationToken.Register(_documents.Cancel);
            await _documents.Pending.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (_documents.Read().Error is { } error) throw error;
            if (_projects.Active == null) throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception failure)
        {
            try { await _documents.StopAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { throw new AggregateException("Editor startup and cleanup failed.", failure, cleanup); }
            throw;
        }
    }

    /// <summary>Updates a borrowed editor view after presentation, including frame acknowledgement and document focus.</summary>
    /// <param name="ui">The parent-owned UI session.</param>
    /// <param name="view">A live view created by this App's editor factory.</param>
    public void Present(UiSession ui, EditorView view)
    {
        ArgumentNullException.ThrowIfNull(ui); ArgumentNullException.ThrowIfNull(view);
        view.VerifyAccess();
        if (!ReferenceEquals(view.DataContext, _model)) throw new ArgumentException("View belongs to another editor App.", nameof(view));
        if(_controllers!=null)view.EnsureViewportController(_controllers);
        view.ViewportController?.Observe(ui);
        var active=view.ViewportController?.IsActive!=false;
        if(active)_viewport.Observe(ui, view);
        if (active && view.Canvas.CanAcknowledgeDraw && view.Canvas.DrawnFrame is { } frame)
        {
            var image = view.Canvas.ImageBounds; var clip = view.Canvas.DrawnClipBounds;
            if (frame.Metadata.PreviewView != null)
                _engine.Preview.Presented(frame, new(image.X, image.Y, image.Width, image.Height),
                    new(clip.X, clip.Y, clip.Width, clip.Height), view.Canvas.LayoutRevision);
            else if (!_engine.Preview.Presented(frame)) _engine.Frames.Presented(frame);
        }
        _model.Refresh(() => _engine.Supervisor.Status, () => _engine.Editor.Status, _commands.Error ?? _engine.Frames.TransportError);
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_diagnostics != null && now >= _nextDiagnosticsRefresh)
        {
            _diagnostics.Refresh();
            _nextDiagnosticsRefresh = now + (long)(_diagnostics.RefreshInterval.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);
        }
        view.Canvas.EmptyText = _projects.Active?.Documents?.Read().Content == null
            ? "Соберите проект и запустите сцену" : "Откройте Edit Preview в строке состояния";
        view.Documents?.SynchronizeFocus(ui);
    }

    /// <summary>Reads an actual receipt admitted by the current runtime/camera and this view's drawn layout. Does not acknowledge another frame.</summary>
    /// <param name="view">Live App-local view on its UI owner.</param><returns>Ready semantic receipt, or null while pending/stale/hidden/foreign.</returns>
    public Preview.PreviewPresentedView? ReadPresentedView(EditorView view)
    {
        ArgumentNullException.ThrowIfNull(view); view.VerifyAccess();
        if (!ReferenceEquals(view.DataContext,_model) || view.IsDisposed || view.ViewportController?.IsActive==false) return null;
        var receipt = _engine.Preview.PresentedView;
        return receipt != null && _engine.Preview.IsCurrent(receipt) &&
            (view.ViewportController==null || receipt.State.Context.ViewId==view.ViewportController.ViewId &&
                receipt.State.Context.DocumentEpoch==_workspace.DocumentEpoch && receipt.State.Context.ContentId==_workspace.ContentToken) &&
            view.Canvas.DrawnFrame?.Metadata.PreviewView == receipt.State.Stamp &&
            ReferenceEquals(view.Canvas.DrawnFrame?.Presentation,receipt.Frame.Presentation) &&
            view.Canvas.MatchesPresentedLayout(receipt.ImageBounds,receipt.ClipBounds,receipt.LayoutRevision) ? receipt : null;
    }

    /// <summary>Applies Save/Discard/Cancel policy before the parent closes its editor UI.</summary>
    /// <returns>False for a user veto or recoverable save/cleanup failure.</returns>
    public Task<bool> RequestCloseAsync() => _documents.RequestExitAsync();

    /// <inheritdoc />
    /// <remarks>Terminal App shutdown drains work and discards remaining unsaved state. Call RequestCloseAsync first for user close.</remarks>
    public Task StopAsync(CancellationToken cancellationToken = default) => _documents.StopAsync();
}
