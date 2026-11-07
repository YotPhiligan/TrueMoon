using System.Collections.Immutable;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Rendering;

/// <summary>One detached draw value with its originating transient entity identity.</summary>
/// <param name="Entity">Source handle, retained as metadata only.</param><param name="Primitive">Copied world matrix and color.</param>
public readonly record struct PrimitiveRenderItem(EntityHandle Entity, RenderPrimitive Primitive);

/// <summary>Immutable detached primitive frame. Holds values, never a World, service, native handle or resource lease.</summary>
public sealed class PrimitiveRenderSnapshot
{
    internal PrimitiveRenderSnapshot(Guid worldId, SceneId sceneId, long revision, ulong tick,
        ImmutableArray<PrimitiveRenderItem> items, PrimitiveScene scene)
    { WorldId = worldId; SceneId = sceneId; Revision = revision; Tick = tick; Items = items; Scene = scene; }
    /// <summary>Gets source World identity; it does not keep the World alive.</summary>
    public Guid WorldId { get; }
    /// <summary>Gets persistent source scene identity.</summary>
    public SceneId SceneId { get; }
    /// <summary>Gets the initial source authoring revision, not a live edit counter.</summary>
    public long Revision { get; }
    /// <summary>Gets the exact extraction/transform tick.</summary>
    public ulong Tick { get; }
    /// <summary>Gets copied primitives in stable slot/generation order.</summary>
    public ImmutableArray<PrimitiveRenderItem> Items { get; }
    /// <summary>Gets the fixed-view frame accepted by the existing primitive renderer.</summary>
    public PrimitiveScene Scene { get; }
}
