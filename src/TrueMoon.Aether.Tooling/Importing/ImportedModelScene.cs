using System.Numerics;
using TrueMoon.Aether.Abstractions.Ecs.Transforms;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Scenes;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Places the persistent occurrences of one committed model import in a scene revision.</summary>
public static class ImportedModelScene
{
    /// <summary>Appends instances and shared mesh definitions with stable IDs and scalar asset references.</summary>
    /// <remarks>Each occurrence uses its captured world pose followed by the optional placement pose.
    /// The v1 scene transform requires positive scale and cannot represent shear or reflection.
    /// A given model can be placed once per scene. The caller owns saving the returned revision.</remarks>
    /// <param name="scene">Existing immutable scene revision.</param>
    /// <param name="model">Committed candidate with permanent ledger IDs.</param>
    /// <param name="placement">Optional row-vector scene placement matrix.</param>
    /// <returns>Next scene revision, with definitions only for meshes used by instances.</returns>
    public static SceneDocument Place(SceneDocument scene, PublishedModelImport model, Matrix4x4? placement = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(model);
        if (scene.Entities.Length + model.Instances.Count > SceneDocumentCodec.MaximumEntities)
            throw new InvalidDataException("Placing the model exceeds the scene entity limit.");

        var entities = scene.Entities.ToBuilder();
        var definitions = scene.Definitions.ToBuilder();
        var schemas = scene.Schemas.ToBuilder();
        var entityIds = scene.Entities.Select(entity => entity.Id).ToHashSet();
        var definitionIds = scene.Definitions.Select(definition => definition.Id).ToHashSet();
        var meshes = model.Meshes.ToDictionary(mesh => mesh.AssetId);
        var addedMeshes = new HashSet<Guid>();
        RequireSchema(BuiltInComponents.Schemas.Single(schema => schema.TypeId == BuiltInComponents.TransformId));
        RequireSchema(MeshRenderingComponents.MeshRendererSchema);
        var pose = placement ?? Matrix4x4.Identity;
        foreach (var instance in model.Instances)
        {
            var mesh = meshes[instance.MeshId];
            var entityId = new EntityId(instance.Id);
            var definitionId = new EntityDefinitionId(mesh.AssetId);
            if (!entityIds.Add(entityId))
                throw new InvalidDataException($"Model occurrence {instance.Id:D} is already in the scene.");
            if (addedMeshes.Add(mesh.AssetId))
            {
                if (!definitionIds.Add(definitionId) || definitions.Count == SceneDocumentCodec.MaximumDefinitions)
                    throw new InvalidDataException("Model definition collides with an existing ID or exceeds the scene definition limit.");
                var renderer = new ComponentData(new(mesh.BindingSetId), MeshRenderingComponents.MeshRendererId, 1,
                    new Dictionary<string, PropertyValue>
                    {
                        ["mesh"] = PropertyValue.From(mesh.AssetId.ToString("D")),
                        ["bindings"] = PropertyValue.From(mesh.BindingSetId.ToString("D"))
                    });
                definitions.Add(new(definitionId, new EntityPatch(addedComponents: [renderer])));
            }

            var transform = CreateTransform(instance, instance.World * pose);
            entities.Add(new(entityId, definitionId, new EntityPatch(addedComponents: [transform])));
        }

        return new(scene.Id, checked(scene.Revision + 1), entities, definitions, schemas);

        void RequireSchema(ComponentSchema expected)
        {
            var current = schemas.SingleOrDefault(schema => schema.TypeId == expected.TypeId);
            if (current == null)
            {
                if (schemas.Count >= SceneDocumentCodec.MaximumSchemas)
                    throw new InvalidDataException("Placing the model exceeds the scene schema limit.");
                schemas.Add(expected);
                return;
            }

            if (current.Version != expected.Version || current.AllowMultiple != expected.AllowMultiple ||
                !current.RequiredTypes.SetEquals(expected.RequiredTypes) || current.Properties.Count != expected.Properties.Count ||
                expected.Properties.Any(pair => !current.Properties.TryGetValue(pair.Key, out var actual) ||
                    actual.Kind != pair.Value.Kind || actual.DefaultValue != pair.Value.DefaultValue ||
                    actual.IsNullable != pair.Value.IsNullable))
                throw new InvalidDataException($"Scene schema {expected.TypeId.Value:D} is incompatible with the model.");
        }
    }

    private static ComponentData CreateTransform(PublishedModelInstance instance, Matrix4x4 matrix)
    {
        if (matrix.M14 != 0 || matrix.M24 != 0 || matrix.M34 != 0 || matrix.M44 != 1 ||
            !Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var position) ||
            scale.X <= 0 || scale.Y <= 0 || scale.Z <= 0)
            throw InvalidTransform();

        var r = Matrix4x4.CreateFromQuaternion(rotation);
        var cosPitch = MathF.Sqrt(r.M31 * r.M31 + r.M33 * r.M33);
        var pitch = MathF.Atan2(-r.M32, cosPitch);
        var yaw = cosPitch > 1e-6f ? MathF.Atan2(r.M31, r.M33) : MathF.Atan2(-r.M13, r.M11);
        var roll = cosPitch > 1e-6f ? MathF.Atan2(r.M12, r.M22) : 0;
        var local = new LocalTransform(position, Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll), scale);
        var rebuilt = local.CreateMatrix();
        // Matrix4x4.Decompose can accept near-orthogonal shear. Check the actual serialized pose.
        if (!Matches(matrix.M11, matrix.M12, matrix.M13, rebuilt.M11, rebuilt.M12, rebuilt.M13, scale.X) ||
            !Matches(matrix.M21, matrix.M22, matrix.M23, rebuilt.M21, rebuilt.M22, rebuilt.M23, scale.Y) ||
            !Matches(matrix.M31, matrix.M32, matrix.M33, rebuilt.M31, rebuilt.M32, rebuilt.M33, scale.Z))
            throw InvalidTransform();

        return new(new(instance.Id), BuiltInComponents.TransformId, 1, new Dictionary<string, PropertyValue>
        {
            ["x"] = PropertyValue.From((double)position.X), ["y"] = PropertyValue.From((double)position.Y), ["z"] = PropertyValue.From((double)position.Z),
            ["pitch"] = PropertyValue.From((double)pitch), ["yaw"] = PropertyValue.From((double)yaw), ["roll"] = PropertyValue.From((double)roll),
            ["sx"] = PropertyValue.From((double)scale.X), ["sy"] = PropertyValue.From((double)scale.Y), ["sz"] = PropertyValue.From((double)scale.Z)
        });

        InvalidDataException InvalidTransform() => new($"Model occurrence {instance.Id:D} requires a representable positive-scale scene transform.");
    }

    private static bool Matches(float x, float y, float z, float actualX, float actualY, float actualZ, float scale) =>
        MathF.Abs(x - actualX) <= scale * 0.0001f && MathF.Abs(y - actualY) <= scale * 0.0001f &&
        MathF.Abs(z - actualZ) <= scale * 0.0001f;
}
