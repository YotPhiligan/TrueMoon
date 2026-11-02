using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TrueMoon.Aether.Editor.Projects;

/// <summary>Strict version1/2/3 JSON project files, bounded to 64 KiB. Paths use the manifest directory, never the process CWD.</summary>
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
        if (description.FormatVersion is not (1 or 2 or ProjectDescription.CurrentVersion))
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
        if (description.Dependencies == null || description.Dependencies.Count > 15)
            throw new InvalidDataException("dependencies must contain at most15 private DLL names.");
        if (description.FormatVersion == 1 && description.Dependencies.Count != 0)
            throw new InvalidDataException("Private dependencies require project formatVersion2.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { description.EntryAssembly };
        foreach (var dependency in description.Dependencies)
        {
            Text(dependency, 128, "dependency");
            if (!dependency.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || dependency.Length == 4 ||
                dependency.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || dependency.Contains('/') || dependency.Contains('\\') || !names.Add(dependency))
                throw new InvalidDataException("dependencies require distinct flat .dll names, excluding entryAssembly.");
        }
        var game = ResolvePath(root, description.GameProject, "gameProject");
        var scenes = ResolvePath(root, description.ScenesRoot, "scenesRoot");
        var content = ResolvePath(root, description.ContentRoot, "contentRoot");
        if (description.PackageDirectory != null && description.FormatVersion < 3)
            throw new InvalidDataException("packageDirectory requires project formatVersion3.");
        var packages = description.PackageDirectory == null ? null : ResolvePath(root, description.PackageDirectory, "packageDirectory");
        if (packages != null && !Directory.Exists(packages)) throw new DirectoryNotFoundException("Package directory is missing: " + packages);
        if (!game.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("gameProject must point to a .csproj file.");
        if (!File.Exists(game)) throw new FileNotFoundException("Game project is missing. Update gameProject or restore the file.", game);
        if (!Directory.Exists(scenes)) throw new DirectoryNotFoundException($"Scenes directory is missing: '{scenes}'.");
        if (!Directory.Exists(content)) throw new DirectoryNotFoundException($"Content directory is missing: '{content}'.");
        string Relative(string value) => Path.GetRelativePath(root, value).Replace('\\', '/');
        var normalized = description with { Name = description.Name.Trim(), GameProject = Relative(game),
            ScenesRoot = Relative(scenes), ContentRoot = Relative(content), PackageDirectory = packages == null ? null : Relative(packages), Dependencies = description.Dependencies.Count == 0
                ? Array.Empty<string>() : Array.AsReadOnly(description.Dependencies.ToArray()) };
        return new(normalized, path, root, game, scenes, content);
    }

    /// <inheritdoc />
    public async Task WriteNewAsync(ResolvedProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();
        var candidate = Resolve(project.ManifestPath, project.Description);
        var json = JsonSerializer.SerializeToNode(candidate.Description, Json)!.AsObject();
        if (candidate.Description.Dependencies.Count == 0) json.Remove("dependencies");
        if (candidate.Description.PackageDirectory == null) json.Remove("packageDirectory");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(json, Json);
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
