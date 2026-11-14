namespace TrueMoon.Aether.Abstractions.Mods;

/// <summary>Detached bounded system state; contains no live Type, delegate, instance or exception.</summary>
public sealed class ModSystemState
{
    /// <summary>Per-system v1 state budget.</summary>
    public const int MaximumBytes = 64 * 1024;
    private readonly byte[] _bytes;
    /// <summary>Copies a versioned payload before retaining it.</summary><param name="version">Positive system-defined schema version.</param><param name="bytes">Complete bounded state.</param>
    public ModSystemState(int version, ReadOnlySpan<byte> bytes)
    {
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        if (bytes.Length > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(bytes));
        Version = version; _bytes = bytes.ToArray();
    }
    /// <summary>System-defined state schema version.</summary>
    public int Version { get; }
    /// <summary>Detached read-only bytes; restore receives an independent host copy.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes;
}
