namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>Explicit executable and argument template for an external source editor; no shell is used.</summary>
public sealed class SourceEditorOptions
{
    /// <summary>Absolute path to the editor executable.</summary>
    public string ExecutablePath { get; set; } = "";
    /// <summary>Individual arguments with literal {file}, {line} and {column} substitutions. At least one must contain {file}.</summary>
    public IReadOnlyList<string> Arguments { get; set; } = [];

    internal SourceEditorOptions Snapshot()
    {
        if (string.IsNullOrWhiteSpace(ExecutablePath) || ExecutablePath.Length > 4096 || !Path.IsPathFullyQualified(ExecutablePath))
            throw new ArgumentException("An absolute source editor executable path is required.", nameof(ExecutablePath));
        ArgumentNullException.ThrowIfNull(Arguments);
        if (Arguments.Count is < 1 or > 32 || Arguments.Any(argument => argument == null || argument.Length > 2048) ||
            !Arguments.Any(argument => argument.Contains("{file}", StringComparison.Ordinal)))
            throw new ArgumentException("Expected up to32 bounded arguments, including a {file} placeholder.", nameof(Arguments));
        return new() { ExecutablePath = Path.GetFullPath(ExecutablePath), Arguments = Array.AsReadOnly(Arguments.ToArray()) };
    }
}
