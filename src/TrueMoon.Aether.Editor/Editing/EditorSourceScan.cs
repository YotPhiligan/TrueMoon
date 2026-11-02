namespace TrueMoon.Aether.Editor.Editing;

internal sealed record EditorSourceScan(bool Changed, bool Available, bool Recoverable, string? Reason, IReadOnlyList<string> ChangedPaths)
{
    internal static EditorSourceScan Same { get; } = new(false, true, true, null, Array.Empty<string>());
    internal static EditorSourceScan Difference(params string[] paths) => new(true, true, true, null, Array.AsReadOnly(paths.Take(16).ToArray()));
    internal static EditorSourceScan Unavailable(string reason, bool recoverable = true) => new(false, false, recoverable, reason, Array.Empty<string>());
}
