namespace TrueMoon.Aether.Scenes;

/// <summary>App-local authoring resolution using an injected catalog resolver factory.</summary>
/// <param name="resolvers">Creates one resolver for each frozen document catalog.</param>
public sealed class SceneResolutionService(IFactory<EntityResolver, SceneDocument> resolvers)
{
    /// <summary>Validates and resolves one immutable authoring document.</summary>
    /// <param name="scene">Document to resolve.</param>
    /// <param name="token">Cancellation between resolution operations.</param>
    /// <returns>The complete resolved snapshot.</returns>
    public ResolvedSceneSnapshot Resolve(SceneDocument scene, CancellationToken token = default)
        => SceneResolver.Resolve(scene, resolvers, token);
}
