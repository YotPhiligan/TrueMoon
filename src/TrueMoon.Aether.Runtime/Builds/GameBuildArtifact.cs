using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Runtime.Builds;

/// <summary>A hashed, immutable-on-publication managed game build. No game code is loaded by validation.</summary>
public sealed class GameBuildArtifact
{
    /// <summary>The bounded artifact manifest file name.</summary>
    public const string ManifestFile = "game-build.json";
    /// <summary>Fixed payload name for builds with captured render resources.</summary>
    public const string RenderContentFile = "render-content.bin";
    internal static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly Manifest _manifest;
    private GameBuildArtifact(string directory, string id, Manifest manifest, RenderContentManifest? renderContent)
    { DirectoryPath = directory; BuildId = id; _manifest = manifest; RenderContent = renderContent; }
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
    /// <summary>Whether this artifact explicitly shares the concrete Runtime assembly.</summary>
    public bool RuntimeAccess => _manifest.RuntimeAccess;
    /// <summary>Validated immutable render resources keyed to this build, or null for managed-only artifacts.</summary>
    public RenderContentManifest? RenderContent { get; }
    internal IEnumerable<System.Reflection.Assembly> SharedAssemblies => GameSharedContracts.For(RuntimeAccess);
    internal static string RuntimeHash => HashFile(typeof(GameBuildArtifact).Assembly.Location);
    internal static readonly HashSet<string> FrameworkNames = System.IO.Directory.EnumerateFiles(
        Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll").Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase)!;

    /// <summary>Validates the manifest, managed dependency set and optional render payload before returning a build.</summary>
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
        using (var header = JsonDocument.Parse(bytes))
            if (!header.RootElement.TryGetProperty("Schema", out var schema) || schema.GetInt32() is not (2 or 3))
                throw new InvalidDataException("Unsupported game manifest schema. Rebuild the game for the layered Runtime.");
        var manifest = JsonSerializer.Deserialize<Manifest>(bytes, Json) ?? throw new InvalidDataException("Missing game manifest.");
        if (manifest.HostContractId != hostContractId) throw new InvalidDataException("Game build belongs to another host.");
        var content = ValidateManifest(path, manifest, id);
        return new(path, id, manifest, content);
    }

    /// <summary>Pins every artifact file against replacement/deletion until the owned runtime is reaped.</summary>
    public IDisposable Pin()
    {
        var pin = new FilePins();
        try
        {
            var names = _manifest.Files.Select(f => f.Name).Prepend(ManifestFile);
            if (_manifest.Content != null) names = names.Append(RenderContentFile);
            foreach (var name in names)
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
                FrameworkNames.Contains(Path.GetFileNameWithoutExtension(n)) || GameSharedContracts.IsReserved(Path.GetFileNameWithoutExtension(n))))
            throw new InvalidDataException("Declare 1..16 distinct private managed DLL names; framework/Aether copies are forbidden.");
    }
    internal static RenderContentManifest? ValidateManifest(string directory, Manifest manifest, string id)
    {
        if (manifest.TargetFramework != GameBuildProfile.TargetFramework)
            throw new InvalidDataException($"Game manifest targets '{manifest.TargetFramework}'; expected {GameBuildProfile.TargetFramework}. Rebuild the game artifact.");
        if (manifest.Schema != (manifest.Content == null ? 2 : 3) || manifest.RuntimeSha256 != RuntimeHash || manifest.Files == null || manifest.SharedContracts == null ||
            !manifest.SharedContracts.SequenceEqual(GameSharedContracts.Describe(manifest.RuntimeAccess)))
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
                if (!GameSharedContracts.For(manifest.RuntimeAccess).Any(assembly => string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase)) && !FrameworkNames.Contains(name) && !allowed.Contains(name))
                    throw new InvalidDataException("Undeclared game dependency: " + name);
            }
        }
        var expected = manifest.Files.Select(f => f.Name).Append(ManifestFile).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (manifest.Content != null) expected.Add(RenderContentFile);
        if (System.IO.Directory.EnumerateFileSystemEntries(directory).Any(p => !expected.Contains(Path.GetFileName(p))))
            throw new InvalidDataException("Unexpected artifact payload.");
        if (manifest.Content is not { } payload) return null;
        var contentPath = Path.Combine(directory, RenderContentFile);
        RejectLink(contentPath);
        using var input = new FileStream(contentPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (payload.Length is <= 0 or > GameBuildRenderContent.MaximumBytes || input.Length != payload.Length)
            throw new InvalidDataException("Game build render payload length mismatch.");
        var bytes = new byte[(int)payload.Length];
        input.ReadExactly(bytes);
        if (Hash(bytes) != payload.Sha256) throw new InvalidDataException("Game build render payload hash mismatch.");
        return GameBuildRenderContent.Read(bytes, RenderContentHash.Parse(id));
    }
    internal static void ValidateIdentity(string value)
    { if (value is not { Length: 64 } || !value.All(Uri.IsHexDigit)) throw new InvalidDataException("Expected SHA256 host identity."); }
    internal static void ValidateEntry(string type, long revision)
    { if (string.IsNullOrWhiteSpace(type) || type.Length > 512 || type.Contains(',') || revision < 0) throw new InvalidDataException("Invalid game entry type/source revision."); }
    internal static void RejectLink(string path)
    { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Artifact links are unsupported."); }
    internal static string HashFile(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal sealed record BuildFile(string Name, string Sha256, long Length);
    internal sealed record RenderPayload(string Sha256, long Length);
    internal sealed record Manifest(int Schema, string TargetFramework, string HostContractId, string RuntimeSha256,
        bool RuntimeAccess, GameSharedContracts.Descriptor[] SharedContracts,
        string EntryAssembly, string EntryType, long SourceRevision, BuildFile[] Files,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RenderPayload? Content = null);
    private sealed class FilePins : IDisposable
    {
        internal readonly List<FileStream> Streams = [];
        public void Dispose() { foreach (var stream in Streams) stream.Dispose(); Streams.Clear(); }
    }
}
