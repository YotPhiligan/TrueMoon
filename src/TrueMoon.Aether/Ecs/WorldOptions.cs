namespace TrueMoon.Aether.Ecs;

/// <summary>Finite storage limits for a caller-owned World. Configuration is copied before use.</summary>
public sealed class WorldOptions
{
    /// <summary>Gets or sets initially allocated entity slots. Default 64; allowed 0..MaximumEntityCount.</summary>
    public int InitialEntityCapacity { get; set; } = 64;
    /// <summary>Gets or sets the slot budget, including retired slots. Default 65536; allowed 1..1048576.</summary>
    /// <remarks>This limit does not change scene, transport, render-frame or checkpoint limits.</remarks>
    public int MaximumEntityCount { get; set; } = 65536;
    /// <summary>Gets or sets the number of distinct component pools retained until disposal.
    /// Default 128; allowed 1..1024. Removing the last value does not unregister its type.</summary>
    public int MaximumComponentTypes { get; set; } = 128;

    internal WorldOptions Snapshot()
    {
        var copy = (WorldOptions)MemberwiseClone();
        if (copy.MaximumEntityCount is < 1 or > 1048576)
            throw new ArgumentOutOfRangeException(nameof(MaximumEntityCount));
        if (copy.InitialEntityCapacity < 0 || copy.InitialEntityCapacity > copy.MaximumEntityCount)
            throw new ArgumentOutOfRangeException(nameof(InitialEntityCapacity));
        if (copy.MaximumComponentTypes is < 1 or > 1024)
            throw new ArgumentOutOfRangeException(nameof(MaximumComponentTypes));
        return copy;
    }
}
