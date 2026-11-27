using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Runtime.Scenes;

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
        var path = ValidateProjection(source, cancellationToken);
        var worlds = new Dictionary<EntityId, Matrix4x4>(); var entities = new List<SceneEntity>();
        foreach (var id in source.HierarchyOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var entity = source.Entities[id];
                var world = CalculateWorld(entity, worlds);
                var appearance = Role(entity, BuiltInComponents.AppearanceId);
                var interaction = Role(entity, BuiltInComponents.InteractionId);
                var primitive = ProjectPrimitive(world, appearance);
                worlds.Add(id, world);
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

    internal static string ValidateProjection(ResolvedSceneSnapshot source, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(source); token.ThrowIfCancellationRequested();
        var path = SceneResolver.ScenePath(source.Id);
        if (source.Entities.Count > SceneSnapshotAdapter.MaximumEntityCount)
            throw new SceneResolutionException(SceneResolutionError.LegacyEntityLimitExceeded, path, "Runtime scene supports at most 64 entities.");
        foreach (var schema in source.ComponentSchemas) BuiltInComponents.ValidateSchema(schema);
        return path;
    }

    internal static ResolvedComponent? Role(ResolvedEntity entity, ComponentTypeId type)
    {
        ResolvedComponent? role = null;
        foreach (var component in entity.Components.Values)
        {
            if (component.TypeId != type) continue;
            if (role != null) throw new InvalidDataException("A projected component role occurs more than once.");
            role = component;
        }
        return role;
    }

    internal static Matrix4x4 CalculateWorld(ResolvedEntity entity, Dictionary<EntityId, Matrix4x4> worlds)
    {
        var local = Matrix4x4.Identity;
        if (Role(entity, BuiltInComponents.TransformId) is { } transform)
        {
            var scale = new Vector3(Number(transform, "sx"), Number(transform, "sy"), Number(transform, "sz"));
            if (scale.X <= 0 || scale.Y <= 0 || scale.Z <= 0) throw new InvalidDataException("Transform scale must be positive.");
            local = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromYawPitchRoll(
                Number(transform, "yaw"), Number(transform, "pitch"), Number(transform, "roll"))) *
                Matrix4x4.CreateTranslation(Number(transform, "x"), Number(transform, "y"), Number(transform, "z"));
        }
        return entity.ParentId is { } parent ? local * worlds[parent] : local;
    }

    internal static RenderPrimitive ProjectPrimitive(Matrix4x4 world, ResolvedComponent? appearance)
    {
        var primitive = new RenderPrimitive(world, appearance == null ? Vector3.One :
            new(Number(appearance, "r"), Number(appearance, "g"), Number(appearance, "b")));
        primitive.Validate();
        return primitive;
    }

    private static float Number(ResolvedComponent component, string key)
    {
        var result = (float)component.Properties[key].Value.GetNumber();
        if (!float.IsFinite(result)) throw new InvalidDataException($"Unrepresentable transform/material property: {key}.");
        return result;
    }
}
