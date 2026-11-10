using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Rendering.Snapshots;

namespace TrueMoon.Aether.Ecs.Rendering;

// Called only at an externally serialized completed-transform boundary. No World access continues after an await.
internal static class MeshSnapshotExtraction
{
    internal const int HeaderBytes = 256;
    internal readonly record struct Plan(int Count, long Bytes, Matrix4x4 Camera, Matrix4x4 ViewProjection, PerspectiveCamera Settings);

    internal static Plan Prepare(World world, ulong tick, RenderContentManifest content, RenderViewConfiguration view,
        RenderSnapshotOptions limits, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(world.IsDisposed, world);
        ResourceGuard.Require(!world.IsFaulted, RenderResourceError.Closed, "World is faulted.");
        ResourceGuard.Require(world.EntityCount <= 10000, RenderResourceError.LimitExceeded, "Snapshot profile admits at most10000logical entities.");
        var source = view.Camera; PerspectiveCamera settings; Matrix4x4 camera;
        if (source.Entity != default)
        {
            ResourceGuard.Require(source.ExternalWorld == default && source.ExternalSettings == default && world.IsAlive(source.Entity),
                RenderResourceError.InvalidIdentity, "Camera source is ambiguous or stale.");
            settings = world.GetComponent<PerspectiveCamera>(source.Entity);
            camera = Transform(world, source.Entity, tick).Matrix;
        }
        else { settings = source.ExternalSettings; camera = source.ExternalWorld; }
        var projection = settings.CreateViewProjection(camera, view.Width, view.Height);
        var count = 0; var renderers = 0;
        world.Query<MeshRenderer>().ForEach((EntityHandle entity, in MeshRenderer value) =>
        {
            token.ThrowIfCancellationRequested();
            ResourceGuard.Require(++renderers <= RenderWorldBinding.MaximumRenderers, RenderResourceError.LimitExceeded, "Renderer count exceeds the snapshot profile.");
            var row = Validate(world, entity, value, tick, content);
            if (!value.Enabled || (value.Layers & settings.Layers) == 0) return;
            count = checked(count + row.Mesh.Slots.Length);
            ResourceGuard.Require(count <= limits.MaximumDrawRanges, RenderResourceError.LimitExceeded, "Draw range admission exhausted; truncation is forbidden.");
        });
        var bytes = checked(HeaderBytes + (long)count * Unsafe.SizeOf<RenderDrawRange>());
        ResourceGuard.Require(bytes <= limits.MaximumPayloadBytes, RenderResourceError.LimitExceeded, "Snapshot payload admission exhausted.");
        token.ThrowIfCancellationRequested(); return new(count, bytes, camera, projection, settings);
    }

    internal static RenderSnapshotData Capture(Plan plan, Guid viewId, RenderResourceIdentity identity, World world,
        Scenes.SceneId scene, long sourceRevision, ulong tick, ulong sequence, ulong deviceGeneration,
        RenderSnapshotResources resources, RenderViewConfiguration view, CancellationToken token)
    {
        var draws = new RenderDrawRange[plan.Count]; var count = 0;
        world.Query<MeshRenderer>().ForEach((EntityHandle entity, in MeshRenderer value) =>
        {
            token.ThrowIfCancellationRequested(); var row = Validate(world, entity, value, tick, resources.Manifest);
            if (!value.Enabled || (value.Layers & plan.Settings.Layers) == 0) return;
            var handle = resources.Handle(value.Mesh);
            for (var i = 0; i < row.Mesh.Slots.Length; i++)
            {
                ResourceGuard.Require(count < draws.Length, RenderResourceError.Conflict, "World changed between extraction passes.");
                var materialKey = row.Bindings.Bindings[i].Material;
                var material = (OpaqueUnlitMaterialDefinition)resources.Manifest.Resolve(materialKey).Definition;
                draws[count++] = new(entity, row.World.Matrix, row.Bounds, value.Mesh, value.Bindings, materialKey, handle,
                    row.Mesh.Slots[i], material.EffectiveLinearColor(value.TintLinear, value.TintEnabled));
            }
        });
        ResourceGuard.Require(count == draws.Length, RenderResourceError.Conflict, "World changed between extraction passes.");
        Array.Sort(draws, static (left, right) =>
        {
            var entity = left.Entity.Index.CompareTo(right.Entity.Index);
            return entity == 0 ? left.Slot.FirstIndex.CompareTo(right.Slot.FirstIndex) : entity;
        });
        token.ThrowIfCancellationRequested();
        return new(viewId, identity, scene, sourceRevision, tick, sequence, resources.BindingRevision, deviceGeneration, resources.Manifest.Digest,
            view, plan.Camera, plan.ViewProjection, plan.Settings.ClearColorSrgb, ImmutableCollectionsMarshal.AsImmutableArray(draws), plan.Bytes);
    }

    private static (MeshDefinition Mesh, MaterialBindingSetDefinition Bindings, WorldTransform World, RenderBounds Bounds)
        Validate(World world, EntityHandle entity, MeshRenderer value, ulong tick, RenderContentManifest content)
    {
        value.Validate(content);
        ContentValidation.Require(!world.HasComponent<PrimitiveAppearance>(entity), RenderContentError.InvalidComponent, "Primitive Appearance and MeshRenderer are ambiguous.");
        var transform = Transform(world, entity, tick);
        var mesh = (MeshDefinition)content.Resolve(value.Mesh).Definition;
        var bindings = (MaterialBindingSetDefinition)content.Resolve(value.Bindings).Definition;
        var bounds = mesh.Bounds.Transform(transform.Matrix);
        return (mesh, bindings, transform, bounds);
    }
    private static WorldTransform Transform(World world, EntityHandle entity, ulong tick)
    {
        ContentValidation.Require(world.TryGetComponent<WorldTransform>(entity, out var value) && value.Tick == tick,
            RenderContentError.InvalidComponent, "Required current-tick WorldTransform is missing.");
        return value;
    }
}
