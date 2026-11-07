namespace TrueMoon.Aether.Ecs.Loading;

/// <summary>Finite admission counts, separate from codec bytes and intermediate resolution memory.</summary>
public sealed class SceneWorldLoadOptions
{
    /// <summary>Gets or sets maximum scene entities, default4096, allowed1..4096.</summary>
    public int MaximumEntities { get; set; } = 4096;
    /// <summary>Gets or sets maximum resolved component instances, default65536, allowed1..65536.</summary>
    public int MaximumComponentInstances { get; set; } = 65536;
    /// <summary>Gets or sets maximum resolved property values, default262144, allowed0..262144.</summary>
    public int MaximumPropertyValues { get; set; } = 262144;
    /// <summary>Gets or sets exclusively owned prepared resource leases, default4096, allowed0..65536.</summary>
    public int MaximumPreparedResources { get; set; } = 4096;
    internal SceneWorldLoadOptions Snapshot()
    {
        var copy = (SceneWorldLoadOptions)MemberwiseClone();
        if (copy.MaximumEntities is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(MaximumEntities));
        if (copy.MaximumComponentInstances is < 1 or > 65536) throw new ArgumentOutOfRangeException(nameof(MaximumComponentInstances));
        if (copy.MaximumPropertyValues is < 0 or > 262144) throw new ArgumentOutOfRangeException(nameof(MaximumPropertyValues));
        if (copy.MaximumPreparedResources is < 0 or > 65536) throw new ArgumentOutOfRangeException(nameof(MaximumPreparedResources));
        return copy;
    }
}
