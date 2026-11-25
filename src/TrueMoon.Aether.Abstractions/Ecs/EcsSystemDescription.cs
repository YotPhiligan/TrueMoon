using System.Collections.ObjectModel;

namespace TrueMoon.Aether.Ecs;

/// <summary>Mutable composition-time declarations; Freeze creates an independent immutable host plan.</summary>
public sealed class EcsSystemDescription
{
    private readonly HashSet<Type> _reads = [], _writes = [];
    private readonly HashSet<string> _after = new(StringComparer.Ordinal), _before = new(StringComparer.Ordinal);
    /// <summary>Creates metadata without activating a system.</summary><param name="id">1..128 ASCII alphanumeric/._- identifier.</param><param name="phase">Ordered phase.</param>
    public EcsSystemDescription(string id, EcsPhase phase = EcsPhase.Gameplay)
    {
        ValidateId(id); if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        Id = id; Phase = phase;
    }
    /// <summary>Graph-local stable ID.</summary>
    public string Id { get; }
    /// <summary>Ordered phase.</summary>
    public EcsPhase Phase { get; }
    /// <summary>Declares read access, including membership/exclusion checks.</summary><typeparam name="T">Reference-free value component.</typeparam><returns>This description.</returns>
    public EcsSystemDescription Read<T>() where T : struct { ComponentStorage.Validate<T>(); _reads.Add(typeof(T)); return this; }
    /// <summary>Declares read/write access including typed structural commands.</summary><typeparam name="T">Reference-free value component.</typeparam><returns>This description.</returns>
    public EcsSystemDescription Write<T>() where T : struct { ComponentStorage.Validate<T>(); _writes.Add(typeof(T)); return this; }
    /// <summary>Declares a required predecessor.</summary><param name="id">Registered system ID.</param><returns>This description.</returns>
    public EcsSystemDescription After(string id) { Add(_after, id); return this; }
    /// <summary>Declares a required successor.</summary><param name="id">Registered system ID.</param><returns>This description.</returns>
    public EcsSystemDescription Before(string id) { Add(_before, id); return this; }
    /// <summary>Copies all declarations. CLR types are live access metadata, never persisted schema identities.</summary><returns>Independent immutable declarations.</returns>
    public EcsSystemMetadata Freeze() => new(Id, Phase, _reads, _writes, _after, _before);
    private void Add(HashSet<string> target, string id)
    {
        ValidateId(id); if (id == Id) throw new ArgumentException("A system cannot depend on itself.", nameof(id));
        if (!target.Contains(id) && target.Count == 1024) throw new InvalidOperationException("System dependency budget exhausted.");
        target.Add(id);
    }
    private static void ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Length > 128 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '_' and not '-'))
            throw new ArgumentException("System IDs use 1..128 ASCII alphanumeric/._- characters.", nameof(id));
    }
}

/// <summary>Immutable component access/order declarations; contains no factory, resolver, World or runtime owner.</summary>
public sealed class EcsSystemMetadata
{
    internal EcsSystemMetadata(string id, EcsPhase phase, IEnumerable<Type> reads, IEnumerable<Type> writes, IEnumerable<string> after, IEnumerable<string> before)
    {
        Id = id; Phase = phase; Reads = Array.AsReadOnly(reads.ToArray()); Writes = Array.AsReadOnly(writes.ToArray());
        After = Array.AsReadOnly(after.ToArray()); Before = Array.AsReadOnly(before.ToArray());
    }
    /// <summary>System ID.</summary>
    public string Id { get; }
    /// <summary>Ordered phase.</summary>
    public EcsPhase Phase { get; }
    /// <summary>Explicit read declarations.</summary>
    public ReadOnlyCollection<Type> Reads { get; }
    /// <summary>Read/write declarations.</summary>
    public ReadOnlyCollection<Type> Writes { get; }
    /// <summary>Required predecessors.</summary>
    public ReadOnlyCollection<string> After { get; }
    /// <summary>Required successors.</summary>
    public ReadOnlyCollection<string> Before { get; }
    /// <summary>Write access also permits reading.</summary><param name="component">Exact live component type.</param><returns>Whether declared readable.</returns>
    public bool CanRead(Type component) => Reads.Contains(component) || Writes.Contains(component);
    /// <summary>Only explicit write declarations permit mutation.</summary><param name="component">Exact live type.</param><returns>Whether declared writable.</returns>
    public bool CanWrite(Type component) => Writes.Contains(component);
}
