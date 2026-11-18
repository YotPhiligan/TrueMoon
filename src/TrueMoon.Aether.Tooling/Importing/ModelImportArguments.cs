namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Explicit per-operation input and trusted deployment. Factory creation performs no IO.</summary>
/// <param name="Source">Complete detached source captured before import.</param>
/// <param name="Worker">Reviewed published worker and expected closure hashes.</param>
/// <param name="ScratchDirectory">Absolute caller-selected local scratch parent; operation owns one unique child only.</param>
/// <param name="Deadline">Finite total import deadline, at most 30 seconds; defaults to 10 seconds.</param>
public sealed record ModelImportArguments(CapturedModelSource Source, ModelImportWorkerDeployment Worker,
    string ScratchDirectory, TimeSpan? Deadline = null)
{
    /// <summary>Finite positive scale of local mesh positions and bounds. Authored instance transforms retain their coordinates.</summary>
    public float GeometryScale { get; init; } = 1;
}
