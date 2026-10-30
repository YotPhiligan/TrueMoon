namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>A source location from a selected, frozen compiler diagnostic. Coordinates are one-based.</summary>
/// <param name="FilePath">Absolute local source path.</param>
/// <param name="Line">Source line.</param>
/// <param name="Column">Source column.</param>
public sealed record EditorSourceLocation(string FilePath, int Line, int Column);

/// <summary>Optional borrowed host hook for an explicit source-navigation request.</summary>
/// <remarks>Implementations must honor cancellation and finish accepted work before App teardown.
/// Completion acknowledges the request, not that another application's UI has displayed the file.</remarks>
public interface ISourceFileNavigator
{
    /// <summary>Requests opening a source location without blocking the UI owner.</summary>
    /// <param name="location">Frozen compiler location.</param>
    /// <param name="cancellationToken">Application stop cancellation.</param>
    /// <returns>The completion of this navigation request.</returns>
    Task OpenAsync(EditorSourceLocation location, CancellationToken cancellationToken = default);
}
