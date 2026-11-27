using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Scenes.Authoring;

namespace TrueMoon.Aether.Tooling.Preview;

// Captured under the document gate, before any async materialization. A superseded
// candidate can finish reading blobs but cannot acquire the latest presentation identity.
internal readonly record struct CapturedScenePreview(Guid ProjectId, Guid OpeningId, SceneAuthoringState State, SceneRenderAssets? Assets)
{
    internal ResolvedSceneSnapshot Scene => State.Resolved;
    internal Task<PreparedScenePreview> PrepareAsync(CancellationToken token) => SceneRenderAssets.PreparePreviewAsync(this, token);
}

internal sealed class PreparedScenePreview
{
    internal PreparedScenePreview(CapturedScenePreview source, SceneSnapshot primitive) { Source = source; Primitive = primitive; }
    internal PreparedScenePreview(CapturedScenePreview source, PreviewRenderPayload render) { Source = source; Render = render; }
    internal CapturedScenePreview Source { get; }
    internal SceneSnapshot? Primitive { get; }
    internal PreviewRenderPayload? Render { get; }
    internal RenderContentHash? Digest => Render?.Digest;
}
