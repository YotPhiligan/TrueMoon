using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TrueMoon.Aether.Builds;

// Declarative package admission runs entirely before SDK execution. The preparation owns every copy.
internal sealed class GameBuildPackages(string source, string directory, Func<string, CancellationToken, Task> copy,
    Func<string, string> mirror, Action<long> reserve)
{
    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;
    private readonly Dictionary<string, Package> _packages = new(Names);
    private readonly Dictionary<string, string> _versions = new(Names);
    internal sealed record Library(string Name, string Path);
    private sealed record Package(string Id, string Version, string Hash, IReadOnlyList<Library> Libraries, Dictionary<string, string> Dependencies);
    internal IEnumerable<Library> Libraries => _packages.Values.SelectMany(package => package.Libraries);

    internal static void ValidateReference(string id, string version)
    {
        if (id.Length > 100 || !Regex.IsMatch(id, @"^[A-Za-z0-9_][A-Za-z0-9_.-]*$", RegexOptions.CultureInvariant) ||
            id.EndsWith('.') || id.Contains("..", StringComparison.Ordinal))
            throw new NotSupportedException("Frozen packages require literal package IDs.");
        _ = ParseVersion(version.StartsWith('[') && version.EndsWith(']') ? version[1..^1] : version);
    }

    internal async Task<IReadOnlyList<Library>> CaptureAsync(string project, IReadOnlyDictionary<string, string> references,
        IReadOnlyList<Library> inherited, IEnumerable<string> projectNames, CancellationToken token)
    {
        var path = Path.Combine(Path.GetDirectoryName(project)!, "packages.lock.json");
        await copy(path, token).ConfigureAwait(false);
        if (new FileInfo(mirror(path)).Length > GameBuildInputSnapshot.MaximumDocumentBytes) throw new InvalidDataException("Package lock exceeds its document budget.");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(mirror(path), token).ConfigureAwait(false), new() { MaxDepth = 16 });
        RejectDuplicates(document.RootElement);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1) throw new NotSupportedException("Frozen packages require lock schema1.");
        var targets = root.GetProperty("dependencies").EnumerateObject().ToArray();
        if (targets.Length != 1 || targets[0].Name != "net10.0") throw new NotSupportedException("Frozen packages require a single net10.0 lock target without runtime identifiers.");
        var rows = new Dictionary<string, JsonElement>(Names);
        var knownProjects = projectNames.ToHashSet(Names);
        foreach (var row in targets[0].Value.EnumerateObject())
        {
            token.ThrowIfCancellationRequested();
            var type = row.Value.GetProperty("type").GetString();
            if (type == "Project")
            {
                if (!knownProjects.Contains(row.Name)) throw new NotSupportedException("Unknown project in package lock: " + row.Name);
                continue; // Actual project closure is captured independently, never resolved from this row.
            }
            if (type is not ("Direct" or "Transitive") || !rows.TryAdd(row.Name, row.Value) || rows.Count > 16)
                throw new NotSupportedException("Frozen package closure requires at most16 unique Direct/Transitive packages.");
        }
        foreach (var row in rows)
        {
            var version = row.Value.GetProperty("resolved").GetString()!; ValidateReference(row.Key, version); _ = ParseVersion(version);
            var direct = row.Value.GetProperty("type").GetString() == "Direct";
            if (direct != references.ContainsKey(row.Key)) throw new NotSupportedException("Package lock direct references differ from the project: " + row.Key);
            if (direct)
            {
                var requested = references[row.Key].Trim('[', ']');
                if (requested != version || !Satisfies(version, row.Value.GetProperty("requested").GetString()!))
                    throw new NotSupportedException("Package lock version differs from the literal PackageReference: " + row.Key);
            }
            var hash = row.Value.GetProperty("contentHash").GetString()!;
            if (Convert.FromBase64String(hash).Length != 64) throw new InvalidDataException("Package contentHash must be SHA512.");
            if (_versions.TryGetValue(row.Key, out var previous) && previous != version) throw new NotSupportedException("Conflicting package versions across project graph: " + row.Key);
            _versions[row.Key] = version;
            var key = row.Key + "/" + version;
            if (!_packages.TryGetValue(key, out var package))
            {
                if (_packages.Count >= 16) throw new NotSupportedException("Frozen project graph exceeds16 packages.");
                package = await CapturePackageAsync(row.Key, version, hash, token).ConfigureAwait(false); _packages.Add(key, package);
            }
            if (package.Hash != hash) throw new InvalidDataException("Conflicting content hashes across project graph: " + row.Key);
            var dependencies = row.Value.TryGetProperty("dependencies", out var values)
                ? values.EnumerateObject().ToDictionary(value => value.Name, value => value.Value.GetString()!, Names) : new(Names);
            if (dependencies.Count != package.Dependencies.Count || dependencies.Any(dependency =>
                !package.Dependencies.TryGetValue(dependency.Key, out var range) || Bounds(range) != Bounds(dependency.Value)))
                throw new NotSupportedException("Package lock dependency edges differ from the nuspec: " + row.Key);
            foreach (var dependency in dependencies)
                if (!rows.TryGetValue(dependency.Key, out var resolved) || !Satisfies(resolved.GetProperty("resolved").GetString()!, dependency.Value))
                    throw new NotSupportedException("Package lock has an unresolved dependency: " + dependency.Key);
        }
        if (references.Keys.Any(id => !rows.ContainsKey(id))) throw new NotSupportedException("PackageReference is absent from packages.lock.json.");
        var reachable = new HashSet<string>(Names); var visiting = new HashSet<string>(Names);
        void Visit(string id)
        {
            if (visiting.Contains(id)) throw new NotSupportedException("Cyclic package dependencies are unsupported.");
            if (!reachable.Add(id)) return;
            visiting.Add(id);
            foreach (var dependency in _packages[id + "/" + rows[id].GetProperty("resolved").GetString()].Dependencies.Keys) Visit(dependency);
            visiting.Remove(id);
        }
        foreach (var id in references.Keys) Visit(id);
        foreach (var row in rows)
            if (_packages[row.Key + "/" + row.Value.GetProperty("resolved").GetString()].Libraries.Any(inherited.Contains)) Visit(row.Key);
        if (reachable.Count != rows.Count) throw new NotSupportedException("Package lock contains an unreachable package.");
        return inherited.Concat(rows.SelectMany(row => _packages[row.Key + "/" + row.Value.GetProperty("resolved").GetString()].Libraries))
            .Distinct().OrderBy(library => library.Name, StringComparer.Ordinal).ToArray();
    }

    private async Task<Package> CapturePackageAsync(string id, string version, string hash, CancellationToken token)
    {
        var path = Path.Combine(source, id.ToLowerInvariant() + "." + version + ".nupkg");
        await copy(path, token).ConfigureAwait(false);
        await using var stream = new FileStream(mirror(path), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (Convert.ToBase64String(await SHA512.HashDataAsync(stream, token).ConfigureAwait(false)) != hash)
            throw new InvalidDataException("Package content hash differs from packages.lock.json: " + id);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var entries = new Dictionary<string, ZipArchiveEntry>(Names); string? target = null; ZipArchiveEntry? nuspec = null;
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested(); reserve(entry.Length);
            var name = entry.FullName;
            if (name.Length > 512 || name.Contains('\\') || name.StartsWith('/') || name.Split('/').Any(part => part is "." or ".." || part.Contains(':')) ||
                !entries.TryAdd(name, entry) || (entry.ExternalAttributes >> 16 & 0xF000) == 0xA000)
                throw new NotSupportedException("Package requires unique regular relative paths.");
            if (name.EndsWith('/')) continue;
            var parts = name.Split('/');
            if (parts.Length == 3 && parts[0] == "lib" && parts[1] is "net10.0" or "net9.0" or "net8.0" or "netstandard2.1" or "netstandard2.0" &&
                Path.GetExtension(name) is ".dll" or ".xml" or ".pdb")
            {
                if (target != null && target != parts[1]) throw new NotSupportedException("Frozen packages require one lib target without ref/runtime variants.");
                target = parts[1]; continue;
            }
            if (parts.Length == 1 && name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            { if (nuspec != null) throw new NotSupportedException("Package contains multiple nuspec files."); nuspec = entry; continue; }
            if (name is "[Content_Types].xml" or "_rels/.rels" || name.StartsWith("package/services/metadata/core-properties/", StringComparison.Ordinal) && name.EndsWith(".psmdcp", StringComparison.Ordinal) ||
                parts.Length == 1 && (Path.GetExtension(name).ToLowerInvariant() is ".md" or ".txt" or ".png" or ".jpg" or ".jpeg" || name is "LICENSE" or "NOTICE")) continue;
            throw new NotSupportedException("Unsupported package asset (including signatures/build/analyzers/native/content/ref): " + name);
        }
        if (nuspec == null || target == null) throw new NotSupportedException("Package requires a nuspec and managed lib target.");
        if (nuspec.Length > GameBuildInputSnapshot.MaximumDocumentBytes) throw new InvalidDataException("Nuspec exceeds its document budget.");
        using var xmlStream = nuspec.Open();
        using var reader = XmlReader.Create(xmlStream, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = GameBuildInputSnapshot.MaximumDocumentBytes });
        var xml = XDocument.Load(reader); var ns = xml.Root?.Name.Namespace ?? throw new InvalidDataException("Package has no nuspec root.");
        if (xml.Root!.Name.LocalName != "package" || xml.Root.Elements(ns + "metadata").Count() != 1)
            throw new InvalidDataException("Package requires one nuspec metadata element.");
        var metadata = xml.Root.Element(ns + "metadata")!;
        if (metadata.Elements().GroupBy(element => element.Name).Any(group => group.Count() != 1)) throw new InvalidDataException("Duplicate nuspec metadata.");
        if (!Names.Equals(metadata.Element(ns + "id")?.Value, id) || metadata.Element(ns + "version")?.Value != version)
            throw new InvalidDataException("Nuspec identity differs from the package lock.");
        var unsupported = metadata.Elements().FirstOrDefault(element => element.Name.LocalName is "frameworkAssemblies" or "frameworkReferences" or "contentFiles" or "packageTypes" or "references" or "developmentDependency");
        if (unsupported != null) throw new NotSupportedException("Unsupported nuspec metadata: " + unsupported.Name.LocalName);
        var dependencies = new Dictionary<string, string>(Names);
        if (metadata.Element(ns + "dependencies") is { } dependencyRoot)
        {
            if (dependencyRoot.Elements(ns + "group").Any())
            {
                var groups = dependencyRoot.Elements().ToArray();
                var aliases = new[] { target, target.Replace("netstandard", ".NETStandard", StringComparison.Ordinal), target.Replace("net", ".NETCoreApp", StringComparison.Ordinal) };
                if (groups.Length != 1 || groups[0].Name != ns + "group" || !aliases.Contains((string?)groups[0].Attribute("targetFramework"), StringComparer.OrdinalIgnoreCase))
                    throw new NotSupportedException("Package requires one dependency group matching its lib target.");
                dependencyRoot = groups[0];
            }
            foreach (var dependency in dependencyRoot.Elements())
            {
                var dependencyId = (string?)dependency.Attribute("id"); var range = (string?)dependency.Attribute("version");
                if (dependency.Name != ns + "dependency" || dependency.HasElements || dependency.Attributes().Any(attribute => attribute.Name != "id" && attribute.Name != "version") ||
                    dependencyId == null || range == null || !dependencies.TryAdd(dependencyId, range)) throw new NotSupportedException("Unsupported nuspec dependency metadata.");
                _ = Bounds(range);
            }
        }
        var libraries = new List<Library>();
        foreach (var entry in entries.Values.Where(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal) && entry.FullName.EndsWith(".dll", StringComparison.Ordinal)))
        {
            var name = Path.GetFileName(entry.FullName); GameBuildArtifact.ValidateNames([name]);
            var destination = Path.Combine(directory, id.ToLowerInvariant(), version, name); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using (var input = entry.Open())
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                var buffer = new byte[65536]; long read = 0; int count;
                while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                { read += count; if (read > entry.Length) throw new InvalidDataException("Package entry exceeds declared size."); await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false); }
                if (read != entry.Length) throw new InvalidDataException("Package entry is truncated.");
            }
            using var image = File.OpenRead(destination); using var pe = new PEReader(image);
            if (!pe.HasMetadata || pe.PEHeaders.CorHeader == null || (pe.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) == 0 || !pe.GetMetadataReader().IsAssembly ||
                !Names.Equals(pe.GetMetadataReader().GetString(pe.GetMetadataReader().GetAssemblyDefinition().Name), Path.GetFileNameWithoutExtension(name)))
                throw new NotSupportedException("Package DLL must be an IL-only assembly matching its file name.");
            libraries.Add(new(name, destination));
        }
        if (libraries.Count == 0) throw new NotSupportedException("Package has no managed DLLs.");
        return new(id, version, hash, libraries, dependencies);
    }

    private static Version ParseVersion(string value)
    {
        if (!Regex.IsMatch(value, @"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant) || !Version.TryParse(value, out var version))
            throw new NotSupportedException("Frozen packages require stable normalized three-part versions.");
        return version;
    }
    private static (Version? Min, bool MinInclusive, Version? Max, bool MaxInclusive) Bounds(string value)
    {
        value = value.Replace(" ", "", StringComparison.Ordinal);
        if (value.Length == 0) throw new NotSupportedException("Missing package dependency version.");
        if (value[0] is not ('[' or '(')) return (ParseVersion(value), true, null, false);
        if (value[^1] is not (']' or ')')) throw new NotSupportedException("Unsupported package dependency range.");
        var pair = value[1..^1].Split(',');
        if (pair.Length == 1 && value[0] == '[' && value[^1] == ']') { var exact = ParseVersion(pair[0]); return (exact, true, exact, true); }
        if (pair.Length != 2 || pair.All(string.IsNullOrEmpty)) throw new NotSupportedException("Unsupported package dependency range.");
        var min = pair[0].Length == 0 ? null : ParseVersion(pair[0]); var max = pair[1].Length == 0 ? null : ParseVersion(pair[1]);
        if (min != null && max != null && min > max) throw new NotSupportedException("Inverted package dependency range.");
        return (min, value[0] == '[', max, value[^1] == ']');
    }
    private static bool Satisfies(string version, string range)
    {
        var parsed = ParseVersion(version); var bounds = Bounds(range);
        return (bounds.Min == null || parsed > bounds.Min || bounds.MinInclusive && parsed == bounds.Min) &&
            (bounds.Max == null || parsed < bounds.Max || bounds.MaxInclusive && parsed == bounds.Max);
    }
    private static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(Names);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate package lock property."); RejectDuplicates(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) RejectDuplicates(item);
    }
}
