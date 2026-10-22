namespace TrueMoon.Aether.Scenes;

/// <summary>Explicit projection into the existing SceneSnapshot without changing its wire/checkpoint contracts.</summary>
public static class SceneSnapshotAdapter
{
    /// <summary>Existing SceneSnapshot entity-count bound.</summary>
    public const int MaximumEntityCount = 64;
    /// <summary>Projects the complete scene in hierarchy order and retains canonical IDs and revision.</summary>
    /// <param name="source">Atomic authoring snapshot.</param>
    /// <param name="project">World-space pose/interaction mapping; receives the scene to resolve parent-relative data explicitly.</param>
    /// <param name="cancellationToken">Cooperative cancellation before/after each projector call and publication.</param>
    /// <returns>A complete compatibility snapshot, never a partial result.</returns>
    /// <remarks>The projector defines component semantics and world transforms. Definition/hierarchy/provenance
    /// data remain in source; SceneSnapshot carries only ID, XYZ and Interactive. External projector side effects are caller-owned.</remarks>
    public static SceneSnapshot Project(ResolvedSceneSnapshot source, Func<ResolvedSceneSnapshot, ResolvedEntity, SceneEntityProjection> project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(project); cancellationToken.ThrowIfCancellationRequested();
        var path = SceneResolver.ScenePath(source.Id);
        if (source.Entities.Count > MaximumEntityCount)
            throw new SceneResolutionException(SceneResolutionError.LegacyEntityLimitExceeded, path, "SceneSnapshot supports at most 64 entities.");
        var entities = new List<SceneEntity>(source.Entities.Count);
        foreach (var id in source.HierarchyOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SceneEntityProjection value;
            try { value = project(source, source.Entities[id]); value.Validate(); }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                throw new SceneResolutionException(SceneResolutionError.ProjectionFailed, $"{path}/entities/{id.Value:D}/projection",
                    "SceneSnapshot projection failed.", id, error);
            }
            cancellationToken.ThrowIfCancellationRequested();
            entities.Add(new() { Id = id.Value.ToString("D"), X = value.X, Y = value.Y, Z = value.Z, Interactive = value.Interactive });
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(source.Id.Value.ToString("D"), source.Revision, entities);
    }
}
