using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Runtime.Preview
{
    /// <summary>Authoring revision bound to one isolated built-in preview runtime generation.</summary>
    /// <param name="SceneId">Persistent scene identity.</param><param name="Revision">Exact committed document revision.</param>
    /// <param name="Runtime">Preview child identity and generation.</param>
    public sealed record ScenePreviewDocument(SceneId SceneId, long Revision, SessionIdentity Runtime);

    /// <summary>Actual presented frame associated with its exact authoring document and runtime.</summary>
    /// <param name="Document">Document and runtime stamp.</param><param name="Frame">Original surface-issued ticket.</param>
    public sealed record ScenePreviewFrame(ScenePreviewDocument Document, ViewportFrame Frame);
}

