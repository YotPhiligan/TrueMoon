namespace TrueMoon.Aether.Scenes.Authoring;

/// <summary>Creates independent caller-owned authoring sessions. The factory borrows stateless scene resolution;
/// it does not retain products, start a runtime or load game code.</summary>
public sealed class SceneAuthoringSessionFactory : IFactory<ISceneAuthoringSession, SceneDocument>
{
    private readonly SceneResolutionService _resolution;
    private readonly SceneAuthoringOptions _options;

    /// <summary>Copies and validates limits before any session is opened.</summary>
    /// <param name="resolution">Shared resolver for the exact input document catalogs, borrowed by sessions.</param>
    /// <param name="options">Configuration copied at construction.</param>
    public SceneAuthoringSessionFactory(SceneResolutionService resolution, SceneAuthoringOptions options)
    {
        ArgumentNullException.ThrowIfNull(resolution); ArgumentNullException.ThrowIfNull(options);
        _resolution = resolution; _options = options.Snapshot();
    }

    /// <summary>Validates the initial scene before returning a new independent session. Failure publishes no product.</summary>
    /// <param name="arguments">Immutable initial document, identity and revision.</param>
    /// <returns>A caller-owned session. Dispose it before releasing borrowed App dependencies.</returns>
    public ISceneAuthoringSession Create(SceneDocument arguments) => new SceneAuthoringSession(_resolution, arguments, _options);
}
