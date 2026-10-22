using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Builds;

/// <summary>A hashed, immutable-on-publication managed game build. No game code is loaded by validation.</summary>
public sealed class GameBuildArtifact
{
    /// <summary>The bounded artifact manifest file name.</summary>
    public const string ManifestFile = "game-build.json";
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly Manifest _manifest;
    private GameBuildArtifact(string directory, string id, Manifest manifest)
    { DirectoryPath = directory; BuildId = id; _manifest = manifest; }
    /// <summary>Gets the absolute directory, named by the manifest SHA256.</summary>
    public string DirectoryPath { get; }
    /// <summary>Gets the manifest SHA256 used by session identity.</summary>
    public string BuildId { get; }
    /// <summary>Gets the matching executable/host contract identity.</summary>
    public string HostContractId => _manifest.HostContractId;
    /// <summary>Gets the source revision associated with this explicit build result.</summary>
    public long SourceRevision => _manifest.SourceRevision;
    /// <summary>Gets the managed entry DLL file name.</summary>
    public string EntryAssembly => _manifest.EntryAssembly;
    /// <summary>Gets the public IGameSession/IGameSessionEntry implementation type name.</summary>
    public string EntryType => _manifest.EntryType;
    /// <summary>Gets a copy of the declared managed files.</summary>
    public IReadOnlyList<string> Files => Array.AsReadOnly(_manifest.Files.Select(f => f.Name).ToArray());
    internal static string ContractHash => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
        HashFile(typeof(IGameSession).Assembly.Location) + ":" + HashFile(typeof(IFactory<>).Assembly.Location))));
    internal static readonly HashSet<string> FrameworkNames = System.IO.Directory.EnumerateFiles(
        Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll").Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase)!;

    /// <summary>Copies a completed game output into a content-addressed artifact directory.</summary>
    /// <remarks>Only explicitly declared flat managed DLLs are supported. Native libraries/assets are outside ENG3a.</remarks>
    public static GameBuildArtifact Publish(string outputDirectory, string artifactRoot, string hostContractId,
        string entryAssembly, string entryType, long sourceRevision, IEnumerable<string>? dependencies = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory); ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        ValidateIdentity(hostContractId); ValidateEntry(entryType, sourceRevision);
        var names = new[] { entryAssembly }.Concat(dependencies ?? []).ToArray(); ValidateNames(names);
        var root = Path.GetFullPath(artifactRoot); System.IO.Directory.CreateDirectory(root);
        RejectLink(root);
        var staging = Path.Combine(root, ".staging-" + Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(staging);
        try
        {
            var files = new List<BuildFile>();
            foreach (var name in names.Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = Path.Combine(Path.GetFullPath(outputDirectory), name); RejectLink(source);
                var length = new FileInfo(source).Length;
                if (length is <= 0 or > 64 * 1024 * 1024) throw new InvalidDataException("Game DLL size exceeds the artifact limit.");
                var target = Path.Combine(staging, name); File.Copy(source, target);
                files.Add(new(name, HashFile(target), length));
            }
            var manifest = new Manifest(1, "net10.0", hostContractId, ContractHash, entryAssembly, entryType, sourceRevision, files.ToArray());
            ValidateManifest(staging, manifest);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, Json); var id = Hash(bytes);
            File.WriteAllBytes(Path.Combine(staging, ManifestFile), bytes);
            cancellationToken.ThrowIfCancellationRequested();
            var destination = Path.Combine(root, id);
            if (!System.IO.Directory.Exists(destination)) System.IO.Directory.Move(staging, destination);
            return Open(destination, hostContractId, id);
        }
        finally { DeleteOwned(staging, root); }
    }

    /// <summary>Validates manifest, shared contract, complete dependency set and all DLL hashes.</summary>
    public static GameBuildArtifact Open(string directory, string hostContractId, string? expectedBuildId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory); ValidateIdentity(hostContractId);
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)); RejectLink(path);
        var manifestPath = Path.Combine(path, ManifestFile); RejectLink(manifestPath);
        using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 65536) throw new InvalidDataException("Invalid game manifest size.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes); var id = Hash(bytes);
        if (Path.GetFileName(path) != id || expectedBuildId != null && expectedBuildId != id)
            throw new InvalidDataException("Game manifest identity mismatch.");
        var manifest = JsonSerializer.Deserialize<Manifest>(bytes, Json) ?? throw new InvalidDataException("Missing game manifest.");
        if (manifest.HostContractId != hostContractId) throw new InvalidDataException("Game build belongs to another host.");
        ValidateManifest(path, manifest);
        return new(path, id, manifest);
    }

    /// <summary>Pins every artifact file against replacement/deletion until the owned runtime is reaped.</summary>
    public IDisposable Pin()
    {
        var pin = new FilePins();
        try
        {
            foreach (var name in new[] { ManifestFile }.Concat(_manifest.Files.Select(f => f.Name)))
                pin.Streams.Add(new FileStream(Path.Combine(DirectoryPath, name), FileMode.Open, FileAccess.Read, FileShare.Read));
            Open(DirectoryPath, HostContractId, BuildId);
            return pin;
        }
        catch { pin.Dispose(); throw; }
    }

    internal static void ValidateNames(string[] names)
    {
        if (names.Length is < 1 or > 16 || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length ||
            names.Any(n => string.IsNullOrWhiteSpace(n) || n.Length > 128 || n.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                n.Contains('/') || n.Contains('\\') || !n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                FrameworkNames.Contains(Path.GetFileNameWithoutExtension(n)) || n.Equals("TrueMoon.Aether.dll", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("TrueMoon.Contracts.dll", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Declare 1..16 distinct private managed DLL names; framework/Aether copies are forbidden.");
    }
    private static void ValidateManifest(string directory, Manifest manifest)
    {
        if (manifest.Schema != 1 || manifest.TargetFramework != "net10.0" || manifest.ContractSha256 != ContractHash || manifest.Files == null)
            throw new InvalidDataException("Unsupported game manifest or shared contract.");
        ValidateIdentity(manifest.HostContractId); ValidateEntry(manifest.EntryType, manifest.SourceRevision);
        ValidateNames(manifest.Files.Select(f => f?.Name!).ToArray());
        if (!manifest.Files.Any(f => f.Name == manifest.EntryAssembly) || manifest.Files.Sum(f => f.Length) > 128L * 1024 * 1024)
            throw new InvalidDataException("Missing entry DLL or oversized artifact.");
        var allowed = manifest.Files.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            var path = Path.Combine(directory, file.Name); RejectLink(path);
            if (file.Length is <= 0 or > 64 * 1024 * 1024 || new FileInfo(path).Length != file.Length || HashFile(path) != file.Sha256)
                throw new InvalidDataException("Game artifact file hash/length mismatch: " + file.Name);
            using var stream = File.OpenRead(path); using var pe = new PEReader(stream);
            if (!pe.HasMetadata) throw new InvalidDataException("Only managed game DLLs are supported.");
            var metadata = pe.GetMetadataReader();
            if (!metadata.IsAssembly || !string.Equals(metadata.GetString(metadata.GetAssemblyDefinition().Name), Path.GetFileNameWithoutExtension(file.Name), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Game DLL assembly/file name mismatch.");
            foreach (var reference in metadata.AssemblyReferences)
            {
                var name = metadata.GetString(metadata.GetAssemblyReference(reference).Name);
                if (name != "TrueMoon.Aether" && name != "TrueMoon.Contracts" && !FrameworkNames.Contains(name) && !allowed.Contains(name))
                    throw new InvalidDataException("Undeclared game dependency: " + name);
            }
        }
        var expected = manifest.Files.Select(f => f.Name).Append(ManifestFile).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (System.IO.Directory.EnumerateFileSystemEntries(directory).Any(p => !expected.Contains(Path.GetFileName(p))))
            throw new InvalidDataException("Unexpected artifact payload.");
    }
    private static void ValidateIdentity(string value)
    { if (value is not { Length: 64 } || !value.All(Uri.IsHexDigit)) throw new InvalidDataException("Expected SHA256 host identity."); }
    private static void ValidateEntry(string type, long revision)
    { if (string.IsNullOrWhiteSpace(type) || type.Length > 512 || type.Contains(',') || revision < 0) throw new InvalidDataException("Invalid game entry type/source revision."); }
    private static void RejectLink(string path)
    { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Artifact links are unsupported."); }
    internal static string HashFile(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static void DeleteOwned(string path, string root)
    {
        var resolved = Path.GetFullPath(path); var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Cleanup escaped owned build directory.");
        if (System.IO.Directory.Exists(resolved)) System.IO.Directory.Delete(resolved, true);
    }
    private sealed record BuildFile(string Name, string Sha256, long Length);
    private sealed record Manifest(int Schema, string TargetFramework, string HostContractId, string ContractSha256,
        string EntryAssembly, string EntryType, long SourceRevision, BuildFile[] Files);
    private sealed class FilePins : IDisposable
    {
        internal readonly List<FileStream> Streams = [];
        public void Dispose() { foreach (var stream in Streams) stream.Dispose(); Streams.Clear(); }
    }
}
