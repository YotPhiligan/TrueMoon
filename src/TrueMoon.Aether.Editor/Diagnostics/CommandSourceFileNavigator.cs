using System.Diagnostics;
using System.Globalization;

namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>Starts a configured external editor. Its process handle is released; the user's editor process is not App-owned.</summary>
public sealed class CommandSourceFileNavigator : ISourceFileNavigator
{
    private readonly SourceEditorOptions _options;
    /// <summary>Snapshots and validates command configuration before any process is started.</summary>
    /// <param name="options">Executable and individual argument templates.</param>
    public CommandSourceFileNavigator(SourceEditorOptions options)
    { ArgumentNullException.ThrowIfNull(options); _options = options.Snapshot(); }

    /// <inheritdoc />
    public Task OpenAsync(EditorSourceLocation location, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested(); var info = StartInfo(location);
            if (!File.Exists(location.FilePath)) throw new FileNotFoundException("The diagnostic source file is no longer available.", location.FilePath);
            cancellationToken.ThrowIfCancellationRequested();
            using var process = Process.Start(info) ?? throw new IOException("Source editor did not start.");
        }, cancellationToken);
    }

    internal ProcessStartInfo StartInfo(EditorSourceLocation location)
    {
        if (!Path.IsPathFullyQualified(location.FilePath) || location.Line < 1 || location.Column < 1)
            throw new ArgumentException("Expected an absolute source path and positive coordinates.", nameof(location));
        var info = new ProcessStartInfo(_options.ExecutablePath) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in _options.Arguments)
        {
            var value = argument.Replace("{line}", location.Line.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{column}", location.Column.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{file}", location.FilePath, StringComparison.Ordinal);
            if (value.Length > 8192) throw new ArgumentException("Expanded editor argument exceeds its budget.", nameof(location));
            info.ArgumentList.Add(value);
        }
        return info;
    }
}
