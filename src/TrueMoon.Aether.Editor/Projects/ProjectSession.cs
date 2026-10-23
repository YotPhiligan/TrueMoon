using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor.Documents;

namespace TrueMoon.Aether.Editor.Projects;

/// <summary>One active project identity. EditorProjects owns and closes it; consumers borrow it.</summary>
/// <remarks>Document ownership is enabled when UseAetherEditorDocuments precedes project registration. Runtime work remains with AetherHost.</remarks>
public sealed class ProjectSession : IDisposable, IAsyncDisposable
{
    private int _closed;
    /// <summary>Creates a session from prepared project inputs. Application code uses the injected factory.</summary>
    /// <param name="arguments">Validated manifest and selected SDK.</param>
    /// <param name="documents">Optional owned document session, created by the injected project factory.</param>
    public ProjectSession(ProjectSessionArguments arguments, SceneDocumentSession? documents = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Project);
        ArgumentNullException.ThrowIfNull(arguments.Toolchain);
        Project = arguments.Project; Toolchain = arguments.Toolchain; Documents = documents;
    }
    /// <summary>Gets the prepared project, retained for diagnostics even after close.</summary>
    public ResolvedProject Project { get; }
    /// <summary>Gets the SDK observed at open time.</summary>
    public ProjectToolchainInfo Toolchain { get; }
    /// <summary>Gets the borrowed document owner, or null for metadata-only composition.</summary>
    public SceneDocumentSession? Documents { get; }
    /// <summary>Gets whether the owning project service closed this session.</summary>
    public bool IsClosed => Volatile.Read(ref _closed) != 0;
    /// <summary>Maps project metadata to an explicit game build request. Does not build or activate a runtime.</summary>
    /// <param name="sourceRevision">Current source/document revision.</param>
    /// <returns>A build request for the selected project.</returns>
    public GameBuildRequest CreateBuildRequest(long sourceRevision)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        if (sourceRevision < 0) throw new ArgumentOutOfRangeException(nameof(sourceRevision));
        return new(Project.GameProjectPath, Project.Description.EntryAssembly, Project.Description.EntryType, sourceRevision);
    }
    /// <summary>Closes this logical session exactly once. Borrowed store, SDK services and engine are not disposed.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    /// <summary>Terminal forced cleanup; normal user transitions must resolve unsaved content first.</summary>
    /// <returns>Actual document IO drain.</returns>
    public ValueTask DisposeAsync()
    { Interlocked.Exchange(ref _closed, 1); return Documents?.DisposeAsync() ?? ValueTask.CompletedTask; }
}
