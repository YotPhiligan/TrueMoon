namespace TrueMoon.Aether.Editor.Documents;

/// <summary>Saving would replace a file that was not the last observed version.</summary>
public sealed class SceneFileConflictException : IOException
{
    /// <summary>Creates an actionable conflict without changing the file.</summary>
    /// <param name="path">Conflicting file.</param><param name="innerException">Optional filesystem cause.</param>
    public SceneFileConflictException(string path, Exception? innerException = null)
        : base($"Scene '{path}' changed externally or already exists. Reopen it or choose a new SaveAs path.", innerException) => Path = path;
    /// <summary>Gets the conflicting scene path.</summary>
    public string Path { get; }
}
