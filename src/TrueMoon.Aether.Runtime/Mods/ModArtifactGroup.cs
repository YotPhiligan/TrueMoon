using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using TrueMoon.Aether.Abstractions.Mods;

namespace TrueMoon.Aether.Runtime.Mods;

// Validated bytes and open read leases. No collectible Type/Assembly is stored here.
internal sealed class ModArtifactGroup : IDisposable
{
    internal sealed record Image(string ModuleId, string Name, string Identity, string Hash, byte[] Bytes, string[] References);
    internal readonly Dictionary<string, Image> Images = new(StringComparer.OrdinalIgnoreCase);
    internal readonly Dictionary<string, Assembly> Shared = new(StringComparer.OrdinalIgnoreCase);
    internal readonly List<ModManifest> Ordered = [];
    private readonly List<FileStream> _pins = [];
    internal static readonly IReadOnlyDictionary<string, AssemblyName> Framework = Directory.GetFiles(
        Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll").Select(ReadFramework)
        .Where(a => a != null).ToDictionary(a => a!.Name!, a => a!, StringComparer.OrdinalIgnoreCase);

    internal static ModArtifactGroup Open(ModLoadRequest request, ModLoaderOptions options, CancellationToken token)
    {
        var group = new ModArtifactGroup();
        try { group.Read(request, options, token); return group; }
        catch { group.Dispose(); throw; }
    }

    private void Read(ModLoadRequest request, ModLoaderOptions options, CancellationToken token)
    {
        if (!Id(request.RootModuleId)) Fail(ModLoadError.InvalidManifest, "Invalid root module ID.");
        if (request.ArtifactDirectories.Count is 0 || request.ArtifactDirectories.Count > options.MaximumModules)
            Fail(ModLoadError.LimitExceeded, "Module count exceeds admission bounds.");
        foreach (var assembly in options.Shared())
            if (!Shared.TryAdd(assembly.GetName().Name!, assembly)) Fail(ModLoadError.SharedContractMismatch, "Duplicate shared simple name.");
        var sharedHashes = Shared.ToDictionary(p => p.Key, p => ModManifestCodec.Hash(File.ReadAllBytes(p.Value.Location)), StringComparer.OrdinalIgnoreCase);
        var modules = new Dictionary<string, ModManifest>(StringComparer.Ordinal);
        var localNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        long bytes = 0;
        foreach (var directory in request.ArtifactDirectories)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(directory)) Fail(ModLoadError.InvalidManifest, "Empty artifact path.");
            var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            SafePath(path);
            var manifestBytes = ReadPinned(Path.Combine(path, "manifest.json"), options.MaximumManifestBytes, ModLoadError.LimitExceeded);
            if (!string.Equals(Path.GetFileName(path), ModManifestCodec.Hash(manifestBytes), StringComparison.OrdinalIgnoreCase))
                Fail(ModLoadError.HashMismatch, "Artifact directory does not match manifest content address.");
            ModManifest manifest;
            try { manifest = ModManifestCodec.Decode(manifestBytes); }
            catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException) { throw new ModFailure(ModLoadError.InvalidManifest, "Invalid manifest JSON."); }
            Validate(manifest, options);
            if (!modules.TryAdd(manifest.Id, manifest)) Fail(ModLoadError.DependencyConflict, "Duplicate module ID.");
            var actual = Directory.GetFileSystemEntries(path).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (actual.Count != manifest.Files.Length + 1 || !actual.SetEquals(manifest.Files.Select(f => f.Name).Append("manifest.json")))
                Fail(ModLoadError.InvalidManifest, "Artifact directory must contain only its manifest and declared managed files.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); localNames.Add(manifest.Id, names);
            var declaredShared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var shared in manifest.SharedAssemblies)
            {
                AssemblyName identity;
                try { identity = new(shared.Identity); } catch { throw new ModFailure(ModLoadError.InvalidManifest, "Invalid shared assembly identity."); }
                if (identity.Name == null || !declaredShared.Add(identity.Name) || !Shared.TryGetValue(identity.Name, out var host) ||
                    !Same(host.FullName, shared.Identity) || !Same(sharedHashes[identity.Name], shared.Sha256))
                    Fail(ModLoadError.SharedContractMismatch, "Shared API identity/hash is not available from this host.");
            }
            if (!declaredShared.Contains(typeof(IModEntry).Assembly.GetName().Name!) || !declaredShared.Contains(typeof(IFactory<>).Assembly.GetName().Name!))
                Fail(ModLoadError.SharedContractMismatch, "Abstractions and Contracts must be declared shared.");
            foreach (var file in manifest.Files)
            {
                token.ThrowIfCancellationRequested();
                if (file.Length > options.MaximumArtifactBytes - bytes) Fail(ModLoadError.LimitExceeded, "Group byte budget exhausted.");
                var content = ReadPinned(Path.Combine(path, file.Name), file.Length, ModLoadError.HashMismatch);
                bytes += content.LongLength;
                if (content.LongLength != file.Length || !Same(ModManifestCodec.Hash(content), file.Sha256)) Fail(ModLoadError.HashMismatch, "Managed file length/hash differs from manifest.");
                var image = Inspect(manifest.Id, file.Name, file.Sha256, content);
                var simpleName = new AssemblyName(image.Identity).Name!;
                if (Shared.ContainsKey(simpleName) || Framework.ContainsKey(simpleName) || !Same(file.Name, simpleName + ".dll"))
                    Fail(ModLoadError.SharedContractMismatch, "Private file shadows a shared/framework assembly or has an alias name.");
                names.Add(simpleName);
                if (Images.TryGetValue(simpleName, out var other))
                {
                    if (!Same(other.Identity, image.Identity) || !Same(other.Hash, image.Hash)) Fail(ModLoadError.DependencyConflict, "Conflicting private assembly identity/content.");
                }
                else Images.Add(simpleName, image);
            }
            if (!names.Contains(Path.GetFileNameWithoutExtension(manifest.EntryAssembly))) Fail(ModLoadError.InvalidManifest, "Entry assembly is not declared.");
        }
        if (!modules.TryGetValue(request.RootModuleId, out var root)) Fail(ModLoadError.MissingDependency, "Root module is unavailable.");
        var visiting = new HashSet<string>(StringComparer.Ordinal); var visited = new HashSet<string>(StringComparer.Ordinal);
        var closures = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        HashSet<string> Visit(ModManifest module)
        {
            if (visited.Contains(module.Id)) return closures[module.Id];
            if (!visiting.Add(module.Id)) throw new ModFailure(ModLoadError.DependencyConflict, "Cyclic module dependency.");
            if (module.ReloadGroup != root!.ReloadGroup) Fail(ModLoadError.DependencyConflict, "All dependencies must share one WholeGroup policy.");
            var closure = new HashSet<string>(StringComparer.Ordinal) { module.Id };
            foreach (var dependency in module.Dependencies.OrderBy(d => d.Id, StringComparer.Ordinal))
            {
                if (!modules.TryGetValue(dependency.Id, out var target)) Fail(ModLoadError.MissingDependency, "Required module is unavailable.");
                var version = Version.Parse(target!.Version);
                if (version < Version.Parse(dependency.MinimumVersion) || version >= Version.Parse(dependency.MaximumVersionExclusive))
                    Fail(ModLoadError.DependencyConflict, "Required module version is outside its declared interval.");
                closure.UnionWith(Visit(target));
            }
            visiting.Remove(module.Id); visited.Add(module.Id); closures.Add(module.Id, closure); Ordered.Add(module); return closure;
        }
        Visit(root!);
        if (visited.Count != modules.Count) Fail(ModLoadError.DependencyConflict, "Request contains modules outside the root dependency closure.");
        foreach (var module in Ordered)
        {
            var allowed = closures[module.Id].SelectMany(id => localNames[id]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var shared = module.SharedAssemblies.Select(s => new AssemblyName(s.Identity).Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var name in localNames[module.Id])
                foreach (var reference in Images[name].References)
                {
                    var requested = new AssemblyName(reference); var simple = requested.Name!;
                    if (Shared.TryGetValue(simple, out var host))
                    {
                        if (!shared.Contains(simple) || !Same(reference, host.FullName)) Fail(ModLoadError.SharedContractMismatch, "Reference requires an undeclared or incompatible shared API.");
                    }
                    else if (Framework.TryGetValue(simple, out var framework))
                    {
                        if (!CompatibleFramework(requested, framework)) Fail(ModLoadError.DependencyConflict, "Incompatible framework assembly reference.");
                    }
                    else if (!allowed.Contains(simple) || !Images.TryGetValue(simple, out var image)) Fail(ModLoadError.MissingDependency, "Managed reference is not declared in the module dependency closure.");
                    else if (!Same(reference, image.Identity)) Fail(ModLoadError.DependencyConflict, "Private assembly reference identity differs.");
                }
        }
    }

    private byte[] ReadPinned(string path, long maximum, ModLoadError boundsError)
    {
        SafePath(path);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); _pins.Add(stream);
        if (stream.Length > maximum || stream.Length > int.MaxValue) Fail(boundsError, "Artifact file exceeds declared/configured length.");
        var data = new byte[(int)stream.Length]; stream.ReadExactly(data); return data;
    }

    private static Image Inspect(string module, string file, string hash, byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, false); using var pe = new PEReader(stream);
            if (!pe.HasMetadata || pe.PEHeaders.CorHeader == null || (pe.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) == 0)
                Fail(ModLoadError.InvalidManifest, "Only pure managed IL assemblies are supported.");
            var metadata = pe.GetMetadataReader();
            if (!metadata.IsAssembly || metadata.MethodDefinitions.Any(h => (metadata.GetMethodDefinition(h).Attributes & MethodAttributes.PinvokeImpl) != 0))
                Fail(ModLoadError.InvalidManifest, "Native imports and netmodules are unsupported.");
            var definition = metadata.GetAssemblyDefinition();
            var identity = new AssemblyName { Name = metadata.GetString(definition.Name), Version = definition.Version, CultureName = metadata.GetString(definition.Culture) };
            if (!definition.PublicKey.IsNil) identity.SetPublicKey(metadata.GetBlobBytes(definition.PublicKey));
            else identity.SetPublicKeyToken([]);
            var references = metadata.AssemblyReferences.Select(handle =>
            {
                var reference = metadata.GetAssemblyReference(handle);
                var name = new AssemblyName { Name = metadata.GetString(reference.Name), Version = reference.Version, CultureName = metadata.GetString(reference.Culture) };
                var key = metadata.GetBlobBytes(reference.PublicKeyOrToken);
                if ((reference.Flags & AssemblyFlags.PublicKey) != 0) name.SetPublicKey(key); else name.SetPublicKeyToken(key);
                return name.FullName;
            }).ToArray();
            if (references.Length > 256) Fail(ModLoadError.LimitExceeded, "Managed reference count exceeds bounds.");
            return new(module, file, identity.FullName, hash, bytes, references);
        }
        catch (BadImageFormatException) { throw new ModFailure(ModLoadError.InvalidManifest, "Invalid managed assembly metadata."); }
    }

    private static void Validate(ModManifest m, ModLoaderOptions options)
    {
        if (m.FormatVersion != ModManifest.CurrentFormatVersion || m.ApiVersion != ModManifest.CurrentApiVersion) Fail(ModLoadError.UnsupportedApi, "Unsupported manifest/API generation.");
        if (!Id(m.Id) || !Id(m.ReloadGroup) || !VersionText(m.Version) || m.ReloadPolicy != "WholeGroup" ||
            !FileName(m.EntryAssembly) || string.IsNullOrWhiteSpace(m.EntryType) || m.EntryType.Length > 512 ||
            m.Dependencies == null || m.Files == null || m.SharedAssemblies == null)
            Fail(ModLoadError.InvalidManifest, "Invalid module identity, entry, version or policy.");
        if (m.Files!.Length is 0 || m.Files.Length > options.MaximumFilesPerModule || m.Dependencies!.Length > options.MaximumModules || m.SharedAssemblies!.Length > 34)
            Fail(ModLoadError.LimitExceeded, "Manifest catalog exceeds configured bounds.");
        if (m.Files.Any(f => f == null || !FileName(f.Name) || f.Length <= 0 || !Hash(f.Sha256)) || m.Files.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != m.Files.Length ||
            m.SharedAssemblies.Any(s => s == null || s.Identity == null || s.Identity.Length > 1024 || !Hash(s.Sha256)) ||
            m.Dependencies.Any(d => d == null || !Id(d.Id) || !VersionText(d.MinimumVersion) || !VersionText(d.MaximumVersionExclusive) || Version.Parse(d.MinimumVersion) >= Version.Parse(d.MaximumVersionExclusive)) ||
            m.Dependencies.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != m.Dependencies.Length)
            Fail(ModLoadError.InvalidManifest, "Invalid or duplicate manifest catalog entry.");
    }

    internal static bool Id(string? text) => text is { Length: > 0 and <= 128 } && text[0] is >= 'a' and <= 'z' && text.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-');
    private static bool FileName(string? text) => text is { Length: > 4 and <= 128 } && text.EndsWith(".dll", StringComparison.Ordinal) && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') && !text.Contains("..");
    private static bool VersionText(string? text) => text is { Length: > 0 and <= 32 } && Version.TryParse(text, out var v) && v.Build >= 0 && v.Revision == -1 && v.ToString(3) == text;
    private static bool Hash(string? text) => text is { Length: 64 } && text.All(char.IsAsciiHexDigit);
    internal static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    internal static bool CompatibleFramework(AssemblyName requested, AssemblyName actual) => Same(requested.Name, actual.Name) && requested.Version <= actual.Version &&
        Same(requested.CultureName ?? "", actual.CultureName ?? "") && (requested.GetPublicKeyToken() ?? []).SequenceEqual(actual.GetPublicKeyToken() ?? []);
    private static AssemblyName? ReadFramework(string path) { try { return AssemblyName.GetAssemblyName(path); } catch (BadImageFormatException) { return null; } }
    private static void SafePath(string path)
    {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) Fail(ModLoadError.InvalidManifest, "Artifact paths cannot traverse reparse points.");
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(ModLoadError error, string message) => throw new ModFailure(error, message);
    public void Dispose() { foreach (var pin in _pins) pin.Dispose(); _pins.Clear(); Images.Clear(); Ordered.Clear(); Shared.Clear(); }
}
