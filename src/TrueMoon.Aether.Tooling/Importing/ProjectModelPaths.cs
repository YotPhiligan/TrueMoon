namespace TrueMoon.Aether.Tooling.Importing;

internal static class ProjectModelPaths
{
    internal static string Relative(string root, string path, bool allowRoot = false)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("Expected an absolute model path in the active project.");
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return allowRoot && relative == "." ? relative : Normalize(relative);
    }

    internal static string Resolve(string root, string path, bool allowRoot = false) => allowRoot && path == "." ? root :
        Path.GetFullPath(Path.Combine(root, Normalize(path)));

    private static string Normalize(string path)
    {
        try { return ModelSourcePath.Normalize(path); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException)
        { throw new InvalidDataException("Saved model paths must stay relative to the active project.", error); }
    }
}
