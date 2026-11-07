namespace TrueMoon.Aether.Rendering.Resources;

/// <summary>Copied admission limits for service-retained CPU data and one resource device.</summary>
public sealed class RenderResourceOptions
{
    /// <summary>Distinct retained exact CPU keys, including candidates.</summary>
    public int MaximumCpuRevisions { get; set; } = 2048;
    /// <summary>Bytes of retained definition arrays; object/driver overhead is excluded.</summary>
    public long MaximumCpuBytes { get; set; } = 64 * 1024 * 1024;
    /// <summary>Distinct frozen project/build/generation/content scopes.</summary>
    public int MaximumCpuScopes { get; set; } = 64;
    /// <summary>Exclusive CPU lease wrappers, including scene/preparation retains.</summary>
    public int MaximumCpuLeases { get; set; } = 16384;
    /// <summary>Actual device-local allocation bytes, including retiring resources.</summary>
    public long MaximumGeometryBytes { get; set; } = 64 * 1024 * 1024;
    /// <summary>Actual upload/readback allocation bytes.</summary>
    public long MaximumUploadStagingBytes { get; set; } = 16 * 1024 * 1024;
    /// <summary>Owned CPU staging buffers reserved before allocation.</summary>
    public long MaximumCpuStagingBytes { get; set; } = 16 * 1024 * 1024;
    /// <summary>Active preparation operations.</summary>
    public int MaximumActivePreparations { get; set; } = 4;
    /// <summary>Waiting operations, excluding active ones.</summary>
    public int MaximumQueuedPreparations { get; set; } = 16;
    /// <summary>Live or retained failed mesh resource slots.</summary>
    public int MaximumGpuEntries { get; set; } = 2048;
    internal RenderResourceOptions Snapshot()
    {
        if (MaximumCpuRevisions is < 1 or > 2048 || MaximumCpuScopes is < 1 or > 64 || MaximumCpuLeases is < 1 or > 16384 ||
            MaximumGpuEntries is < 1 or > 2048 || MaximumActivePreparations is < 1 or > 4 || MaximumQueuedPreparations is < 0 or > 16 ||
            MaximumCpuBytes is < 0 or > 64 * 1024 * 1024 || MaximumGeometryBytes is < 0 or > 64 * 1024 * 1024 ||
            MaximumUploadStagingBytes is < 0 or > 16 * 1024 * 1024 || MaximumCpuStagingBytes is < 0 or > 16 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(RenderResourceOptions), "Resource limits exceed the v1 profile.");
        return new() { MaximumCpuRevisions = MaximumCpuRevisions, MaximumCpuBytes = MaximumCpuBytes,
            MaximumCpuScopes = MaximumCpuScopes, MaximumCpuLeases = MaximumCpuLeases, MaximumGeometryBytes = MaximumGeometryBytes,
            MaximumUploadStagingBytes = MaximumUploadStagingBytes, MaximumCpuStagingBytes = MaximumCpuStagingBytes,
            MaximumActivePreparations = MaximumActivePreparations, MaximumQueuedPreparations = MaximumQueuedPreparations,
            MaximumGpuEntries = MaximumGpuEntries };
    }
}
