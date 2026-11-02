using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;

namespace TrueMoon.Aether.Editor.Projects;

/// <summary>One active project identity. EditorProjects owns and closes it; consumers borrow it.</summary>
/// <remarks>Document ownership is enabled when UseAetherEditorDocuments precedes project registration. Runtime work remains with AetherHost.</remarks>
public sealed class ProjectSession : IDisposable, IAsyncDisposable
{
    private int _closed;
    private readonly Lock _cleanupGate = new();
    private Task? _cleanup;
    /// <summary>Creates a session from prepared project inputs. Application code uses the injected factory.</summary>
    /// <param name="arguments">Validated manifest and selected SDK.</param>
    /// <param name="documents">Optional owned document session, created by the injected project factory.</param>
    public ProjectSession(ProjectSessionArguments arguments, SceneDocumentSession? documents = null)
        : this(arguments, documents, null) { }

    /// <summary>Creates a project owner for document and inert source-observation products.</summary>
    /// <param name="arguments">Validated project inputs.</param><param name="documents">Optional owned document.</param>
    /// <param name="sourceWatch">Optional owned observer; never owned by the container or a View.</param>
    public ProjectSession(ProjectSessionArguments arguments, SceneDocumentSession? documents, EditorSourceWatch? sourceWatch)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Project);
        ArgumentNullException.ThrowIfNull(arguments.Toolchain);
        Project = arguments.Project; Toolchain = arguments.Toolchain; Documents = documents; SourceWatch = sourceWatch;
        OwnerKey = sourceWatch?.OwnerKey ?? Guid.NewGuid();
    }
    /// <summary>Gets the prepared project, retained for diagnostics even after close.</summary>
    public ResolvedProject Project { get; }
    /// <summary>Gets the SDK observed at open time.</summary>
    public ProjectToolchainInfo Toolchain { get; }
    /// <summary>Gets the borrowed document owner, or null for metadata-only composition.</summary>
    public SceneDocumentSession? Documents { get; }
    /// <summary>Fresh session owner identity, unrelated to the persisted project Guid.</summary>
    public Guid OwnerKey { get; }
    /// <summary>Borrowed source observer, or null for metadata-only/legacy project composition.</summary>
    public EditorSourceWatch? SourceWatch { get; }
    /// <summary>Gets whether the owning project service closed this session.</summary>
    public bool IsClosed => Volatile.Read(ref _closed) != 0;
    /// <summary>Maps project metadata to an explicit game build request. Does not build or activate a runtime.</summary>
    /// <param name="sourceRevision">Current source/document revision.</param>
    /// <returns>A build request for the selected project.</returns>
    public GameBuildRequest CreateBuildRequest(long sourceRevision)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        if (sourceRevision < 0) throw new ArgumentOutOfRangeException(nameof(sourceRevision));
        return new(Project.GameProjectPath, Project.Description.EntryAssembly, Project.Description.EntryType, sourceRevision,
            Project.Description.Dependencies.Count == 0 ? null : Array.AsReadOnly(Project.Description.Dependencies.ToArray()))
        { PackageDirectory = Project.Description.PackageDirectory == null ? null : Path.GetFullPath(Project.Description.PackageDirectory, Project.RootDirectory) };
    }
    /// <summary>Closes this logical session exactly once. Borrowed store, SDK services and engine are not disposed.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    /// <summary>Terminal forced cleanup; normal user transitions must resolve unsaved content first.</summary>
    /// <returns>Actual document IO drain.</returns>
    public ValueTask DisposeAsync()
    {
        lock (_cleanupGate)
        {
            Interlocked.Exchange(ref _closed, 1);
            if (_cleanup == null || _cleanup.IsFaulted) _cleanup = CleanupAsync();
            return new(_cleanup);
        }
    }

    internal void BindSourceBuild(GameBuildArtifact artifact)
    { lock (_cleanupGate) { if (!IsClosed) SourceWatch?.Bind(artifact); } }

    private async Task CleanupAsync()
    {
        List<Exception>? errors = null; var drains = new List<Task>();
        try { if (SourceWatch != null) drains.Add(SourceWatch.DisposeAsync().AsTask()); }
        catch (Exception error) { (errors ??= []).Add(error); }
        try { if (Documents != null) drains.Add(Documents.DisposeAsync().AsTask()); }
        catch (Exception error) { (errors ??= []).Add(error); }
        foreach (var drain in drains)
            try { await drain.ConfigureAwait(false); } catch (Exception error) { (errors ??= []).Add(error); }
        if (errors is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors != null) throw new AggregateException(errors);
    }
}
