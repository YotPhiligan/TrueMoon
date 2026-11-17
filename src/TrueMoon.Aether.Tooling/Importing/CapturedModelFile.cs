using System.Security.Cryptography;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Detached captured bytes. Public readers cannot mutate the retained buffer.</summary>
public sealed class CapturedModelFile
{
    private readonly byte[] _bytes;
    internal CapturedModelFile(string name, byte[] bytes)
    {
        Name = name; _bytes = bytes; Sha256 = Convert.ToHexString(SHA256.HashData(bytes));
    }

    /// <summary>Normalized case-sensitive relative name.</summary>
    public string Name { get; }
    /// <summary>Captured length in bytes.</summary>
    public int Length => _bytes.Length;
    /// <summary>SHA256 of the entire captured file.</summary>
    public string Sha256 { get; }
    /// <summary>Creates an independent read-only cursor. The caller disposes the stream.</summary>
    public Stream OpenRead() => new MemoryStream(_bytes, 0, _bytes.Length, writable: false, publiclyVisible: false);
    /// <summary>Returns a caller-owned copy.</summary>
    public byte[] CopyBytes() => (byte[])_bytes.Clone();
    internal ReadOnlyMemory<byte> Bytes => _bytes;
}
