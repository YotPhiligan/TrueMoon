namespace TrueMoon.Aether.Editor.Projects;

/// <summary>Project persistence boundary. Implementations do not activate game code or own sessions.</summary>
public interface IProjectStore
{
    /// <summary>Reads and fully validates one manifest and its referenced paths.</summary>
    /// <param name="manifestPath">Manifest path, independent of the game working directory.</param>
    /// <param name="cancellationToken">Read cancellation.</param>
    /// <returns>A complete resolved project, never a partial result.</returns>
    Task<ResolvedProject> ReadAsync(string manifestPath, CancellationToken cancellationToken = default);

    /// <summary>Validates metadata and existing files/directories for a new manifest without writing it.</summary>
    /// <param name="manifestPath">Target .aetherproj path in an existing directory.</param>
    /// <param name="description">Portable project metadata.</param>
    /// <returns>Normalized metadata and absolute paths.</returns>
    ResolvedProject Resolve(string manifestPath, ProjectDescription description);

    /// <summary>Atomically publishes a new manifest, never overwriting an existing file.</summary>
    /// <param name="project">Previously prepared project; paths are revalidated before writing.</param>
    /// <param name="cancellationToken">Cancellation before atomic file publication.</param>
    /// <returns>Completion after publication and temporary-file cleanup.</returns>
    Task WriteNewAsync(ResolvedProject project, CancellationToken cancellationToken = default);
}
