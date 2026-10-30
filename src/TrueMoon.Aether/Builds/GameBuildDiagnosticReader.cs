using System.Text.Json;

namespace TrueMoon.Aether.Builds;

/// <summary>Reads only the job-owned C# SARIF2.1 file after the compiler has exited.</summary>
internal static class GameBuildDiagnosticReader
{
    internal const int MaximumBytes = 2 * 1024 * 1024;
    internal const int MaximumEntries = 128;
    internal const string FileName = "compiler.sarif";

    internal static GameBuildDiagnosticBatch Read(string path, string projectDirectory)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > MaximumBytes) return new(GameBuildDiagnosticState.TooLarge, [], IsTruncated: true);
            // Bound the actual read too: an external writer cannot grow the allocation after the length check.
            var bytes = new byte[MaximumBytes + 1]; var count = 0;
            while (count < bytes.Length)
            {
                var read = file.Read(bytes.AsSpan(count)); if (read == 0) break; count += read;
            }
            return count > MaximumBytes ? new(GameBuildDiagnosticState.TooLarge, [], IsTruncated: true)
                : Parse(bytes.AsMemory(0, count), projectDirectory);
        }
        catch (FileNotFoundException) { return new(GameBuildDiagnosticState.Unavailable, []); }
        catch (DirectoryNotFoundException) { return new(GameBuildDiagnosticState.Unavailable, []); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { return new(GameBuildDiagnosticState.Failed, [], FailureType: error.GetType().Name); }
    }

    internal static GameBuildDiagnosticBatch Parse(ReadOnlyMemory<byte> bytes, string projectDirectory)
    {
        if (bytes.Length > MaximumBytes) return new(GameBuildDiagnosticState.TooLarge, [], IsTruncated: true);
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
            var root = document.RootElement;
            if (Text(root, "version") != "2.1.0" || !root.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
                return new(GameBuildDiagnosticState.Failed, [], FailureType: "UnsupportedSarif");
            var entries = new List<GameBuildDiagnostic>(); var omitted = 0; var truncated = false;
            foreach (var run in runs.EnumerateArray())
            {
                if (!run.TryGetProperty("results", out var results)) continue;
                if (results.ValueKind != JsonValueKind.Array) throw new JsonException("Invalid results.");
                foreach (var result in results.EnumerateArray())
                {
                    if (entries.Count == MaximumEntries) { omitted++; truncated = true; continue; }
                    var code = Bound(Text(result, "ruleId") ?? "", 128, ref truncated);
                    var message = result.TryGetProperty("message", out var text) ? Text(text, "text") ?? "" : "";
                    message = Bound(message, 2048, ref truncated);
                    var severity = Text(result, "level") switch
                    {
                        "error" => GameBuildDiagnosticSeverity.Error,
                        "warning" or null => GameBuildDiagnosticSeverity.Warning, // SARIF's default result level.
                        "note" => GameBuildDiagnosticSeverity.Information,
                        "none" => GameBuildDiagnosticSeverity.Hidden,
                        _ => GameBuildDiagnosticSeverity.Unknown
                    };
                    string? path = null; int? line = null, column = null;
                    if (result.TryGetProperty("locations", out var locations) && locations.ValueKind == JsonValueKind.Array && locations.GetArrayLength() > 0 &&
                        locations[0].TryGetProperty("physicalLocation", out var location))
                    {
                        if (location.TryGetProperty("artifactLocation", out var artifact) && !artifact.TryGetProperty("uriBaseId", out _))
                        {
                            var uri = Text(artifact, "uri");
                            if (uri?.Length > 4096) truncated = true;
                            else
                            {
                                path = LocalPath(uri, projectDirectory);
                                if (path?.Length > 4096) { path = null; truncated = true; }
                            }
                        }
                        if (location.TryGetProperty("region", out var region) && Number(region, "startLine") is > 0 and var startLine)
                        { line = startLine; column = Number(region, "startColumn") is > 0 and var startColumn ? startColumn : 1; }
                    }
                    entries.Add(new(code, severity, message, path, line, column));
                }
            }
            return new(GameBuildDiagnosticState.Available, entries.AsReadOnly(), omitted, truncated);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        { return new(GameBuildDiagnosticState.Failed, [], FailureType: error.GetType().Name); }
    }

    private static string? Text(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int? Number(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : null;
    private static string Bound(string value, int maximum, ref bool truncated)
    {
        if (value.Length <= maximum) return value;
        truncated = true; if (char.IsHighSurrogate(value[maximum - 1])) maximum--;
        return value[..maximum];
    }
    private static string? LocalPath(string? value, string directory)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return uri.IsFile && !uri.IsUnc ? Path.GetFullPath(uri.LocalPath) : null;
            var path = Path.GetFullPath(Uri.UnescapeDataString(value), directory);
            return path.StartsWith("\\\\", StringComparison.Ordinal) ? null : path;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or UriFormatException) { return null; }
    }
}
