using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TrueMoon.Aether.Builds;

/// <summary>Immutable witness of evaluated SDK inputs and the preprocessed imported project.</summary>
/// <remarks>This detects observed changes, not transient edit-and-revert or arbitrary target/generator/environment IO.
/// It is process-local provenance; reopening an artifact does not recreate or trust this witness.</remarks>
public sealed class GameBuildInputSnapshot
{
    internal const int MaximumDocumentBytes = 8 * 1024 * 1024;
    internal const int MaximumFiles = 4096;
    internal const long MaximumFileBytes = 64L * 1024 * 1024, MaximumTotalBytes = 256L * 1024 * 1024;
    private static readonly StringComparer Paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private GameBuildInputSnapshot(GameBuildRequest request, string evaluation, string project, IReadOnlyList<GameBuildInputFile> files,
        GameBuildPreparationSnapshot? preparation, string job, bool hasProjectReferences)
    {
        Request = request with { Dependencies = Array.AsReadOnly(request.Dependencies?.ToArray() ?? []) };
        EvaluationHash = evaluation; PreprocessedHash = project; Files = files; Preparation = preparation;
        HasProjectReferences = hasProjectReferences;
        Fingerprint = Hash(JsonSerializer.SerializeToUtf8Bytes(new { Request.ProjectPath, Request.EntryAssembly, Request.EntryType,
            Dependencies = Request.Dependencies?.ToArray() ?? [], EvaluationHash, PreprocessedHash,
            Files = Files.Select(file => file with { Path = file.Path.Replace(job, "$JOB", OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) }),
            Preparation = preparation?.Fingerprint }));
    }
    internal GameBuildRequest Request { get; }
    internal bool HasProjectReferences { get; }
    /// <summary>Content identity, excluding the caller's compile ticket and owned temporary job paths.</summary>
    public string Fingerprint { get; }
    /// <summary>Canonical evaluated properties/items, retaining item order and ignoring file timestamps.</summary>
    public string EvaluationHash { get; }
    /// <summary>Imported-project content identity with owned temporary job paths normalized.</summary>
    public string PreprocessedHash { get; }
    /// <summary>Read-only source/resource/analyzer/hint-reference and configuration file identities.</summary>
    public IReadOnlyList<GameBuildInputFile> Files { get; }
    /// <summary>Pre-evaluation closure identity when frozen preparation was requested; null for the legacy trusted preparation profile.</summary>
    public GameBuildPreparationSnapshot? Preparation { get; }
    /// <summary>Process-local original-source projection from frozen capture; never reconstructed by artifact Open.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public GameBuildSourceWatchDescriptor? SourceWatch => Preparation?.SourceWatch;

    internal static GameBuildInputSnapshot Create(GameBuildRequest request, string query, string preprocessed, string job,
        CancellationToken token = default, GameBuildPreparation? preparation = null)
    {
        token.ThrowIfCancellationRequested();
        if (Encoding.UTF8.GetByteCount(query) > MaximumDocumentBytes || Encoding.UTF8.GetByteCount(preprocessed) > MaximumDocumentBytes)
            throw new InvalidDataException("SDK input witness exceeds its document limit.");
        var fullJob = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(job));
        string Normalize(string value)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            value = preparation?.Original(value) ?? value;
            value = value.Replace(SecurityElement.Escape(fullJob)!, "$JOB", comparison).Replace(fullJob, "$JOB", comparison)
                .Replace(fullJob.Replace('\\', '/'), "$JOB", comparison);
            return preparation == null ? value : value.Replace(fullJob[System.IO.Path.GetPathRoot(fullJob)!.Length..], "$JOB", comparison);
        }
        using var document = JsonDocument.Parse(query);
        var root = document.RootElement;
        var properties = root.GetProperty("Properties"); var items = root.GetProperty("Items");
        if (properties.GetProperty("TargetFramework").GetString() != GameBuildProfile.TargetFramework)
            throw new InvalidDataException($"Tracked builds require {GameBuildProfile.TargetFramework}.");
        foreach (var name in new[] { "ProjectReference", "PackageReference" })
            if (items.GetProperty(name).GetArrayLength() != 0) throw new NotSupportedException("Tracked inputs currently require a single project with explicit managed HintPath references; " + name + " is unsupported.");

        var paths = new HashSet<string>(Paths) { preparation?.CopyPath(request.ProjectPath) ?? System.IO.Path.GetFullPath(request.ProjectPath) };
        var projectDirectory = System.IO.Path.GetDirectoryName(preparation?.CopyPath(request.ProjectPath) ?? request.ProjectPath)!;
        foreach (var group in items.EnumerateObject())
        {
            token.ThrowIfCancellationRequested();
            foreach (var item in group.Value.EnumerateArray())
            {
                string? path;
                if (group.Name == "Reference")
                {
                    path = item.TryGetProperty("HintPath", out var hint) ? hint.GetString() : null;
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        var name = item.GetProperty("Identity").GetString()!.Split(',')[0];
                        if (!GameBuildArtifact.FrameworkNames.Contains(name)) throw new NotSupportedException("Tracked managed references require HintPath: " + name);
                        continue;
                    }
                }
                else if (group.Name is "ProjectReference" or "PackageReference" or "CompilerVisibleProperty" or "CompilerVisibleItemMetadata") continue;
                else path = item.GetProperty("FullPath").GetString();
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("SDK input has no path.");
                var fullPath = System.IO.Path.GetFullPath(path, projectDirectory);
                // The SDK lists potential ancestor analyzer configs even when they do not exist.
                // Their paths remain in the evaluation hash; later creation adds a file identity.
                if (group.Name is "EditorConfigFiles" or "GlobalAnalyzerConfigFiles" && !File.Exists(fullPath)) continue;
                paths.Add(fullPath);
            }
        }
        for (var directory = new DirectoryInfo(projectDirectory); directory != null; directory = directory.Parent)
        {
            foreach (var name in new[] { ".editorconfig", "global.json", "NuGet.Config", "nuget.config", "Directory.Build.rsp", "MSBuild.rsp" })
            {
                token.ThrowIfCancellationRequested(); var path = System.IO.Path.Combine(directory.FullName, name);
                if (File.Exists(path)) paths.Add(path);
            }
            if (preparation?.IsBoundary(directory.FullName) == true) break;
        }
        if (paths.Count > MaximumFiles) throw new InvalidDataException("Too many tracked build input files.");
        var files = new List<GameBuildInputFile>(); long total = 0;
        var buffer = new byte[64 * 1024];
        foreach (var path in paths.Order(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            var original = preparation?.Original(path) ?? path;
            if (preparation != null && preparation.Files.TryGetValue(original, out var frozen))
            {
                files.Add(frozen); continue;
            }
            if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new NotSupportedException("Tracked build inputs must be regular files: " + path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = stream.Length;
            if (length > MaximumFileBytes || total > MaximumTotalBytes - length) throw new InvalidDataException("Tracked build inputs exceed their byte limit.");
            total += length; using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long read = 0; int count;
            while ((count = stream.Read(buffer)) != 0)
            {
                token.ThrowIfCancellationRequested(); read += count;
                if (read > length) throw new IOException("A build input changed while being read: " + path);
                hash.AppendData(buffer, 0, count);
            }
            if (read != length) throw new IOException("A build input changed while being read: " + path);
            files.Add(new(original, length, Convert.ToHexString(hash.GetHashAndReset())));
        }
        using var canonical = new MemoryStream();
        using (var writer = new Utf8JsonWriter(canonical)) { Write(root, writer); writer.Flush(); }
        token.ThrowIfCancellationRequested();
        return new(request, Hash(canonical.ToArray()), Hash(Encoding.UTF8.GetBytes(Normalize(preprocessed))),
            Array.AsReadOnly(files.OrderBy(f => f.Path, StringComparer.Ordinal).ToArray()), preparation?.Snapshot, fullJob, preparation?.References.Count > 0);

        void Write(JsonElement value, Utf8JsonWriter writer)
        {
            token.ThrowIfCancellationRequested();
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject(); var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate SDK input metadata.");
                        if (property.Name is "ModifiedTime" or "CreatedTime" or "AccessedTime") continue;
                        writer.WritePropertyName(property.Name); Write(property.Value, writer);
                    }
                    writer.WriteEndObject(); break;
                case JsonValueKind.Array:
                    writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(item, writer); writer.WriteEndArray(); break;
                case JsonValueKind.String: writer.WriteStringValue(Normalize(value.GetString()!)); break;
                default: value.WriteTo(writer); break;
            }
        }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
