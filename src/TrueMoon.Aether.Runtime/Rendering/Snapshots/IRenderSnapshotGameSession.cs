using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Runtime.Rendering.Snapshots;

/// <summary>Explicit required mesh-view capability; legacy primitive games do not implement it.</summary>
/// <remarks>Runtime serializes source access, tick and capture. The source borrows World, never transfers its ownership to presentation.</remarks>
public interface IRenderSnapshotGameSession : IGameSession
{
    /// <summary>Returns the frozen content, borrowed completed World and explicit versioned camera at the current tick.</summary>
    RenderSceneSource ReadRenderScene();
}

/// <summary>Serialized runtime-only source, distinct from detached snapshot/Mod SDK/UI data.</summary>
public sealed record RenderSceneSource(World World, RenderContentManifest Content, SceneId SceneId, long SourceRevision,
    RenderCameraSource Camera, ulong CameraRevision = 1);
