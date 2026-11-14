namespace TrueMoon.Kton;

/// <summary>Immutable admission limits. Values may be reduced; raising version-one ceilings requires a new profile and evidence.</summary>
public sealed record PhysicsLimits
{
    /// <summary>Static, dynamic and kinematic bodies together.</summary>
    public int MaxBodies { get; init; } = 1024;
    /// <summary>Live immutable shape records, including shared shapes once.</summary>
    public int MaxShapes { get; init; } = 2048;
    /// <summary>Live joints. A policy ceiling, not a measured 4096-joint workload.</summary>
    public int MaxJoints { get; init; } = 4096;
    /// <summary>Commands admitted between successful steps, including repeated batches.</summary>
    public int MaxCommandsPerTick { get; init; } = 4096;
    /// <summary>Capacity of each raw-sample, active-pair, pending-End and published-event buffer; overflow must be observable.</summary>
    public int MaxContactEvents { get; init; } = 8192;
    /// <summary>Maximum returned hits per query; insufficient output is reported as truncation.</summary>
    public int MaxQueryHits { get; init; } = 256;
    /// <summary>Hit-buffer and conservative traversal-stack payload budget in bytes; excludes retained allocator blocks and fixed ABI locals.</summary>
    public int MaxQueryScratchBytes { get; init; } = 1024 * 1024;
    /// <summary>Vertices in a single captured static mesh.</summary>
    public int MaxMeshVertices { get; init; } = 65536;
    /// <summary>Triangles in a single captured static mesh.</summary>
    public int MaxMeshTriangles { get; init; } = 65536;
    /// <summary>Input vertices and indices combined, excluding backend cooking allocations.</summary>
    public int MaxMeshInputBytes { get; init; } = 16 * 1024 * 1024;
    /// <summary>Diagnostic sampled backend pool threshold in bytes per world; not a hard RAM cap.</summary>
    public long PoolDiagnosticBudgetBytes { get; init; } = 16 * 1024 * 1024;
}
