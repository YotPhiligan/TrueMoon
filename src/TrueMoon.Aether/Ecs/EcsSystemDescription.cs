namespace TrueMoon.Aether.Ecs;

/// <summary>Mutable composition-time system metadata, frozen when registered.</summary>
public sealed class EcsSystemDescription
{
    private readonly HashSet<Type> _reads = [], _writes = [];
    private readonly HashSet<string> _after = new(StringComparer.Ordinal), _before = new(StringComparer.Ordinal);
    /// <summary>Creates metadata without activating a system.</summary><param name="id">Distinct ASCII alphanumeric/._- identifier, 1..128 characters.</param><param name="phase">Execution phase.</param>
    public EcsSystemDescription(string id, EcsPhase phase = EcsPhase.Gameplay)
    {
        ValidateId(id);
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        Id = id; Phase = phase;
    }
    /// <summary>Gets the stable graph-local system identifier.</summary>
    public string Id { get; }
    /// <summary>Gets the ordered phase.</summary>
    public EcsPhase Phase { get; }
    /// <summary>Declares readonly access, including exclusion membership checks.</summary><typeparam name="T">Component type.</typeparam><returns>This description.</returns>
    public EcsSystemDescription Read<T>() where T : struct { World.ValidateComponent<T>(); _reads.Add(typeof(T)); return this; }
    /// <summary>Declares read/write access, including recorded typed commands.</summary><typeparam name="T">Component type.</typeparam><returns>This description.</returns>
    public EcsSystemDescription Write<T>() where T : struct { World.ValidateComponent<T>(); _writes.Add(typeof(T)); return this; }
    /// <summary>Requires a registered predecessor, within this phase or an earlier phase.</summary><param name="id">Predecessor ID.</param><returns>This description.</returns>
    public EcsSystemDescription After(string id) { AddDependency(_after, id); return this; }
    /// <summary>Requires a registered successor, within this phase or a later phase.</summary><param name="id">Successor ID.</param><returns>This description.</returns>
    public EcsSystemDescription Before(string id) { AddDependency(_before, id); return this; }
    private void AddDependency(HashSet<string> target, string id)
    {
        ValidateId(id);
        if (id == Id) throw new ArgumentException("A system cannot depend on itself.", nameof(id));
        if (!target.Contains(id) && target.Count == 1024) throw new InvalidOperationException("System dependency budget exhausted.");
        target.Add(id);
    }
    internal SystemPlan Snapshot() => new(Id, Phase, new(_reads), new(_writes), [.. _after], [.. _before]);
    private static void ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (id.Length > 128 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '_' and not '-'))
            throw new ArgumentException("System IDs use 1..128 ASCII alphanumeric/._- characters.", nameof(id));
    }
}

internal sealed record SystemPlan(string Id, EcsPhase Phase, HashSet<Type> Reads, HashSet<Type> Writes, string[] After, string[] Before)
{
    internal bool CanRead(Type type) => Reads.Contains(type) || Writes.Contains(type);
}
