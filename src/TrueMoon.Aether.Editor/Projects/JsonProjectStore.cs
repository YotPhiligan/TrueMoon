using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TrueMoon.Aether.Editor.Projects;

/// <summary>Strict version-1 JSON project files, bounded to 64 KiB. Paths use the manifest directory, never the process CWD.</summary>
/// <remarks>Projects are trusted local inputs. Lexical path containment does not establish a filesystem security sandbox over links.</remarks>
public sealed class JsonProjectStore : IProjectStore
{
    /// <summary>Maximum manifest byte length.</summary>
    public const int MaximumBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    /// <inheritdoc />
    public async Task<ResolvedProject> ReadAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        var path = ManifestPath(manifestPath);
        cancellationToken.ThrowIfCancellationRequested();
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var bytes = new byte[MaximumBytes + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await input.ReadAsync(bytes.AsMemory(count), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            count += read;
        }
        if (count > MaximumBytes) throw new InvalidDataException($"Project '{path}' exceeds {MaximumBytes} bytes.");
        ProjectDescription description;
        try
        {
            var offset = count >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            using var document = JsonDocument.Parse(bytes.AsMemory(offset, count - offset), new() { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a project object.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
                if (!names.Add(property.Name)) throw new JsonException($"Duplicate property '{property.Name}'.");
            description = document.RootElement.Deserialize<ProjectDescription>(Json)
                ?? throw new JsonException("Expected a project description.");
        }
        catch (JsonException error) { throw new InvalidDataException($"Invalid project JSON in '{path}': {error.Message}", error); }
        cancellationToken.ThrowIfCancellationRequested();
        return Resolve(path, description);
    }

    /// <inheritdoc />
    public ResolvedProject Resolve(string manifestPath, ProjectDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        var path = ManifestPath(manifestPath);
        var root = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Create the project directory '{root}' first.");
        if (description.FormatVersion != ProjectDescription.CurrentVersion)
            throw new InvalidDataException($"Unsupported project format {description.FormatVersion}; expected {ProjectDescription.CurrentVersion}.");
        if (description.Id == Guid.Empty) throw new InvalidDataException("Project id must be nonempty.");
        Text(description.Name, 128, "name");
        Text(description.EntryAssembly, 128, "entryAssembly");
        Text(description.EntryType, 512, "entryType");
        if (!description.EntryAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            description.EntryAssembly.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            description.EntryAssembly.Contains('/') || description.EntryAssembly.Contains('\\') ||
            description.EntryAssembly.Length == 4)
            throw new InvalidDataException("entryAssembly must be a flat .dll file name.");
        if (!Regex.IsMatch(description.EntryType, @"^[\p{L}_][\p{L}\p{N}_]*(?:[.+][\p{L}_][\p{L}\p{N}_]*)*$",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            throw new InvalidDataException("entryType must be a qualified non-generic type name.");
        var game = ResolvePath(root, description.GameProject, "gameProject");
        var scenes = ResolvePath(root, description.ScenesRoot, "scenesRoot");
        var content = ResolvePath(root, description.ContentRoot, "contentRoot");
        if (!game.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("gameProject must point to a .csproj file.");
        if (!File.Exists(game)) throw new FileNotFoundException("Game project is missing. Update gameProject or restore the file.", game);
        if (!Directory.Exists(scenes)) throw new DirectoryNotFoundException($"Scenes directory is missing: '{scenes}'.");
        if (!Directory.Exists(content)) throw new DirectoryNotFoundException($"Content directory is missing: '{content}'.");
        string Relative(string value) => Path.GetRelativePath(root, value).Replace('\\', '/');
        var normalized = description with { Name = description.Name.Trim(), GameProject = Relative(game),
            ScenesRoot = Relative(scenes), ContentRoot = Relative(content) };
        return new(normalized, path, root, game, scenes, content);
    }

    /// <inheritdoc />
    public async Task WriteNewAsync(ResolvedProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();
        var candidate = Resolve(project.ManifestPath, project.Description);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(candidate.Description, Json);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Serialized project exceeds the manifest limit.");
        var temporary = candidate.ManifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Exception? failure = null;
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous))
            {
                await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, candidate.ManifestPath, overwrite: false);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); }
        }
    }

    private static string ManifestPath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var path = Path.GetFullPath(value);
        if (!path.EndsWith(".aetherproj", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Expected a .aetherproj manifest.", nameof(value));
        return path;
    }

    private static void Text(string value, int maximum, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
            throw new InvalidDataException($"{field} requires 1..{maximum} characters.");
    }

    private static string ResolvePath(string root, string value, string field)
    {
        Text(value, 512, field);
        if (Path.IsPathRooted(value) || value.Contains(':')) throw new InvalidDataException($"{field} must be relative to the project.");
        var path = Path.GetFullPath(value.Replace('\\', Path.DirectorySeparatorChar), root);
        if (!path.Equals(root, StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{field} escapes the project directory.");
        return path;
    }
}
