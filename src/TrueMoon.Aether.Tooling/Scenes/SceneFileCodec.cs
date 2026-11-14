using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Tooling.Scenes.Authoring;

namespace TrueMoon.Aether.Tooling.Scenes;

/// <summary>Authoring admission over the common Runtime scene byte grammar.</summary>
public sealed class SceneFileCodec : SceneDocumentCodec
{
    private readonly IFactory<ISceneAuthoringSession, SceneDocument> _authoring;
    public SceneFileCodec(IFactory<ISceneAuthoringSession, SceneDocument> authoring)
        : base(new SceneResolutionService(new Factory<EntityResolver, SceneDocument>(scene => new(scene.Definitions, scene.Schemas))))
        => _authoring = authoring ?? throw new ArgumentNullException(nameof(authoring));
    protected override void Validate(SceneDocument document)
    {
        if (document.Entities.Length > MaximumEntities || document.Definitions.Length > MaximumDefinitions || document.Schemas.Length > MaximumSchemas)
            throw new InvalidDataException("Scene catalog limit exceeded.");
        using var validation = _authoring.Create(document) ?? throw new InvalidOperationException("Authoring factory returned null.");
        _ = validation.Read();
    }
}
