using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Documents;

namespace TrueMoon.Aether.Tooling.Hosting;

/// <summary>App-owned headless build owner borrowing the project coordinator. It never creates a RuntimeEngine, window or graphics device.</summary>
/// <remarks>Build results are candidates; successful Build does not start or replace a game. The compiler product belongs
/// only to this host. Project/document/source products belong to the separate App-owned project coordinator.</remarks>
public sealed class ToolingHost : IDisposable, IAsyncDisposable
{
    private readonly Lock _gate = new();
    private Task? _build, _cleanup;
    private bool _closed;
    private GameBuildResult? _lastGood;
    private Guid _lastOwner;
    private readonly bool _runtimeAccess;

    internal ToolingHost(EditorProjects projects, GameBuildCompiler builds, bool runtimeAccess)
    {
        ArgumentNullException.ThrowIfNull(projects); ArgumentNullException.ThrowIfNull(builds);
        Projects = projects; Builds = builds; _runtimeAccess = runtimeAccess;
    }

    /// <summary>Borrowed App-owned project coordinator; it owns active/retired sessions, documents and source observers.</summary>
    public EditorProjects Projects { get; }
    /// <summary>Borrowed compiler owned only by this host. Low-level callers may build explicit requests directly.</summary>
    public GameBuildCompiler Builds { get; }
    /// <summary>Last successful selected Build for the current project opening. Failed or cancelled candidates never replace it.</summary>
    public GameBuildResult? LastGoodBuild
    { get { lock (_gate) return Projects.Active?.OwnerKey == _lastOwner ? _lastGood : null; } }
    /// <summary>Whether terminal App shutdown closed Build admission.</summary>
    public bool IsClosed { get { lock (_gate) return _closed; } }

    /// <summary>Builds a frozen candidate for the current opening and binds its source baseline after a successful owner check.</summary>
    /// <param name="sourceRevision">Caller-selected nonnegative source revision.</param>
    /// <param name="cancellationToken">Cancellation of this owned compiler operation.</param>
    /// <returns>The selected artifact with compiler/preparation provenance. Never starts a runtime.</returns>
    public Task<GameBuildResult> BuildAsync(long sourceRevision, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<GameBuildResult> completion;
        ProjectSession project;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (_build is { IsCompleted: false }) throw new InvalidOperationException("A Tooling Build is already active.");
            project = Projects.Active ?? throw new InvalidOperationException("Open a project before Build.");
            var request = project.CreateBuildRequest(sourceRevision);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _build = completion.Task;
            _ = CompleteBuildAsync(project, request, cancellationToken, completion);
        }
        return completion.Task;
    }

    private async Task CompleteBuildAsync(ProjectSession project, GameBuildRequest request, CancellationToken token,
        TaskCompletionSource<GameBuildResult> completion)
    {
        try
        {
            var candidate = await Builds.BuildAsync(request with
            { RuntimeAccess = _runtimeAccess, TrackInputs = true, CaptureCompilation = true, CapturePreparation = true, CaptureProjectReferences = true, ReuseArtifact = true }, token).ConfigureAwait(false);
            lock (_gate)
            {
                token.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(_closed, this);
                if (!ReferenceEquals(Projects.Active, project) || project.IsClosed)
                    throw new InvalidOperationException("The project opening changed while Build was running; the candidate was not selected.");
                project.BindSourceBuild(candidate);
                _lastOwner = project.OwnerKey; _lastGood = candidate;
            }
            completion.TrySetResult(candidate);
        }
        catch (OperationCanceledException error) { completion.TrySetCanceled(error.CancellationToken); }
        catch (Exception error) { completion.TrySetException(error); }
    }

    /// <summary>Closes admission and drains the compiler before terminally discarding unsaved in-memory project products.</summary>
    /// <returns>Shared cleanup completion. Failed cleanup retains its owners for explicit retry.</returns>
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _closed = true;
            Builds.CloseAdmission();
            if (_cleanup is null || _cleanup.IsFaulted) _cleanup = CleanupAsync();
            return new(_cleanup);
        }
    }
    private async Task CleanupAsync()
    {
        List<Exception> failures = [];
        try { await Builds.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { failures.Add(error); }
        Task? build;
        lock (_gate) build = _build;
        if (build != null) try { await build.ConfigureAwait(false); } catch { /* The caller receives the operation failure. */ }
        try { await Projects.CloseAsync(DocumentCloseChoice.Discard).ConfigureAwait(false); } catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0) throw new AggregateException("Headless Tooling cleanup failed; retry the retained owners.", failures);
    }
    /// <summary>Synchronously completes the same owned cleanup. Prefer DisposeAsync during App shutdown.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
