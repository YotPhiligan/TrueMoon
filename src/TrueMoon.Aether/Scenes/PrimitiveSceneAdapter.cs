using System.Numerics;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Scenes;

/// <summary>Projects built-in component roles into the bounded control v3 scene consumed by a runtime.</summary>
public static class PrimitiveSceneAdapter
{
    /// <summary>Resolves local S*R*T and then local*parentWorld without decomposing matrices or losing shear.</summary>
    /// <param name="source">Complete resolved authoring scene. Unknown component types remain authoring-only.</param>
    /// <param name="cancellationToken">Cancellation before each entity and before atomic publication.</param>
    /// <returns>Deep-owned snapshot retaining scene/entity identity and exact scene revision.</returns>
    /// <remarks>Appearance and Interaction require Transform. Entities without Appearance do not draw.
    /// Euler rotation uses Quaternion.CreateFromYawPitchRoll. Positive scales only; nonfinite/singular results are rejected.
    /// No runtime world edits or implicit component-schema migration occur.</remarks>
    public static SceneSnapshot Project(ResolvedSceneSnapshot source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); cancellationToken.ThrowIfCancellationRequested();
        var path = SceneResolver.ScenePath(source.Id);
        if (source.Entities.Count > SceneSnapshotAdapter.MaximumEntityCount)
            throw new SceneResolutionException(SceneResolutionError.LegacyEntityLimitExceeded, path, "Runtime scene supports at most 64 entities.");
        foreach (var schema in source.ComponentSchemas) BuiltInComponents.ValidateSchema(schema);
        var worlds = new Dictionary<EntityId, Matrix4x4>(); var entities = new List<SceneEntity>();
        foreach (var id in source.HierarchyOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var entity = source.Entities[id];
                ResolvedComponent? Role(ComponentTypeId type) => entity.Components.Values.SingleOrDefault(c => c.TypeId == type);
                var transform = Role(BuiltInComponents.TransformId); var appearance = Role(BuiltInComponents.AppearanceId);
                var interaction = Role(BuiltInComponents.InteractionId);
                var local = Matrix4x4.Identity;
                if (transform != null)
                {
                    var scale = new Vector3(Number(transform, "sx"), Number(transform, "sy"), Number(transform, "sz"));
                    if (scale.X <= 0 || scale.Y <= 0 || scale.Z <= 0) throw new InvalidDataException("Transform scale must be positive.");
                    local = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromYawPitchRoll(
                        Number(transform, "yaw"), Number(transform, "pitch"), Number(transform, "roll"))) *
                        Matrix4x4.CreateTranslation(Number(transform, "x"), Number(transform, "y"), Number(transform, "z"));
                }
                var world = entity.ParentId is { } parent ? local * worlds[parent] : local;
                var primitive = new RenderPrimitive(world, appearance == null ? Vector3.One :
                    new(Number(appearance, "r"), Number(appearance, "g"), Number(appearance, "b")));
                primitive.Validate(); worlds.Add(id, world);
                entities.Add(new() { Id = id.Value.ToString("D"), X = world.M41, Y = world.M42, Z = world.M43,
                    Interactive = interaction?.Properties["enabled"].Value.GetBoolean() ?? false,
                    Primitive = appearance == null ? null : ScenePrimitive.FromPrimitive(primitive) });
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                throw new SceneResolutionException(SceneResolutionError.ProjectionFailed, $"{path}/entities/{id.Value:D}/primitive",
                    "Built-in primitive projection failed.", id, error);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(source.Id.Value.ToString("D"), source.Revision, entities);
    }
    private static float Number(ResolvedComponent component, string key)
    {
        var result = (float)component.Properties[key].Value.GetNumber();
        if (!float.IsFinite(result)) throw new InvalidDataException($"Unrepresentable transform/material property: {key}.");
        return result;
    }
}
