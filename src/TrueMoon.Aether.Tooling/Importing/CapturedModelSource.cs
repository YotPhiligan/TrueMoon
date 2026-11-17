using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Complete immutable input for a later importer. This is not decoded geometry or a runtime content manifest.</summary>
public sealed class CapturedModelSource
{
    internal CapturedModelSource(Guid sourceId, string rootName, IEnumerable<CapturedModelFile> files)
    {
        SourceId = sourceId; RootName = rootName;
        Files = Array.AsReadOnly(files.OrderBy(file => file.Name, StringComparer.Ordinal).ToArray());
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "TrueMoon.ModelSourceCapture.v1"); Append(hash, sourceId.ToString("N")); Append(hash, rootName);
        foreach (var file in Files) { Append(hash, file.Name); Append(hash, file.Bytes.Span); }
        InputRevision = Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>Persistent identity supplied by the caller, independent of source paths and hashes.</summary>
    public Guid SourceId { get; }
    /// <summary>Normalized root name.</summary>
    public string RootName { get; }
    /// <summary>Versioned SHA256 of identity, root name and ordinal-sorted names/bytes; not a full import/cache or mesh revision.</summary>
    public string InputRevision { get; }
    /// <summary>Complete files sorted by ordinal relative name; no physical handles are retained.</summary>
    public IReadOnlyList<CapturedModelFile> Files { get; }
    /// <summary>Gets an exact case-sensitive captured name, without disk lookup.</summary>
    public CapturedModelFile GetFile(string name)
    {
        var normalized = ModelSourcePath.Normalize(name);
        return Files.FirstOrDefault(file => file.Name == normalized) ?? throw new FileNotFoundException("The name is not captured.", name);
    }

    private static void Append(IncrementalHash hash, string value) => Append(hash, Encoding.UTF8.GetBytes(value));
    private static void Append(IncrementalHash hash, ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[4]; BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length); hash.AppendData(bytes);
    }
}
