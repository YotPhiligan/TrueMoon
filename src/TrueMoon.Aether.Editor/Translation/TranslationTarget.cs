using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Translation;

/// <summary>Frozen authoring identity, inverse parent basis and ghost geometry; never owns an authoring session.</summary>
public sealed class TranslationTarget
{
    private TranslationTarget(SceneDocument document, EntityId entity, ComponentId component, TranslationPosition original,
        Matrix4x4 inverseParent, Matrix4x4 world, ImmutableArray<PreviewPickPrimitive> ghosts)
    { Document = document; Entity = entity; Component = component; Original = original; InverseParent = inverseParent; World = world; Ghosts = ghosts; }
    /// <summary>Exact immutable document instance required at release.</summary>
    public SceneDocument Document { get; }
    /// <summary>Selected entity.</summary>
    public EntityId Entity { get; }
    /// <summary>Exact built-in Transform occurrence.</summary>
    public ComponentId Component { get; }
    /// <summary>Original local double values.</summary>
    public TranslationPosition Original { get; }
    /// <summary>Inverse parent matrix; transform deltas with its linear part only.</summary>
    public Matrix4x4 InverseParent { get; }
    /// <summary>Actual projected selected cube matrix.</summary>
    public Matrix4x4 World { get; }
    /// <summary>Selected cube and visible descendants, from the same frozen pick payload.</summary>
    public ImmutableArray<PreviewPickPrimitive> Ghosts { get; }

    /// <summary>Captures a visible exact-schema target from the same authoring and actual pick source.</summary>
    /// <param name="state">Coherent current document/resolved state.</param><param name="picks">Actual immutable geometry.</param>
    /// <param name="entity">Selected entity.</param><param name="target">Detached target.</param><returns>False on missing/mismatched/invalid content.</returns>
    public static bool TryCapture(SceneAuthoringState state, PreviewPickSnapshot picks, EntityId entity, out TranslationTarget? target)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(picks); target = null;
        if (picks.GeometryProfile != PreviewPickSnapshot.CubeGeometryProfile || picks.Scene != state.Document.Id || picks.Revision != state.Document.Revision ||
            !state.Resolved.Entities.TryGetValue(entity, out var resolved)) return false;
        var primitive = picks.Primitives.FirstOrDefault(p => p.Entity == entity);
        if (primitive.Entity != entity || entity.Value == Guid.Empty) return false;
        var component = resolved.Components.Values.SingleOrDefault(c => c.TypeId == BuiltInComponents.TransformId && c.SchemaVersion == 1);
        if (component == null) return false;
        double Number(string key) => component.Properties[key].Value.GetNumber();
        var original = new TranslationPosition(Number("x"), Number("y"), Number("z"));
        var inverseParent = Matrix4x4.Identity;
        if (resolved.ParentId != null)
        {
            var local = Matrix4x4.CreateScale((float)Number("sx"), (float)Number("sy"), (float)Number("sz")) *
                Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromYawPitchRoll((float)Number("yaw"), (float)Number("pitch"), (float)Number("roll"))) *
                Matrix4x4.CreateTranslation((float)original.X, (float)original.Y, (float)original.Z);
            if (!Matrix4x4.Invert(primitive.World, out var inverseWorld)) return false;
            inverseParent = inverseWorld * local;
        }
        if (!Finite(inverseParent)) return false;
        var descendants = new HashSet<EntityId> { entity };
        foreach (var id in state.Resolved.HierarchyOrder)
            if (state.Resolved.Entities[id].ParentId is { } parent && descendants.Contains(parent)) descendants.Add(id);
        target = new(state.Document, entity, component.Id, original, inverseParent, primitive.World,
            picks.Primitives.Where(p => descendants.Contains(p.Entity)).ToImmutableArray()); return true;
    }

    /// <summary>Converts a constrained world delta to canonical local position with double intermediates.</summary>
    /// <param name="delta">World delta.</param><param name="position">Original values plus parent-local delta.</param>
    /// <returns>False for overflow/nonfinite coordinates. Zero delta returns exact original doubles.</returns>
    public bool TryPosition(Vector3 delta, out TranslationPosition position)
    {
        position = default;
        if (!TranslationDrag.Finite(delta)) return false;
        var m = InverseParent;
        var value = new TranslationPosition(Original.X + (double)delta.X * m.M11 + (double)delta.Y * m.M21 + (double)delta.Z * m.M31,
            Original.Y + (double)delta.X * m.M12 + (double)delta.Y * m.M22 + (double)delta.Z * m.M32,
            Original.Z + (double)delta.X * m.M13 + (double)delta.Y * m.M23 + (double)delta.Z * m.M33);
        if (!Valid(value)) return false;
        position = value; return true;
    }

    /// <summary>Creates only changed scalar overrides, preserving all rotation/scale/inheritance intent.</summary>
    /// <param name="position">Representable canonical candidate.</param><returns>Zero to three changed property edits.</returns>
    public ImmutableArray<PropertyEdit> CreateEdits(TranslationPosition position)
    {
        if (!Valid(position)) throw new ArgumentOutOfRangeException(nameof(position));
        var edits = ImmutableArray.CreateBuilder<PropertyEdit>();
        if (position.X != Original.X) edits.Add(new(Entity, Component, "x", PropertyValue.From(position.X)));
        if (position.Y != Original.Y) edits.Add(new(Entity, Component, "y", PropertyValue.From(position.Y)));
        if (position.Z != Original.Z) edits.Add(new(Entity, Component, "z", PropertyValue.From(position.Z)));
        return edits.ToImmutable();
    }
    private static bool Valid(TranslationPosition p) => float.IsFinite((float)p.X) && float.IsFinite((float)p.Y) && float.IsFinite((float)p.Z);
    private static bool Finite(Matrix4x4 m) => new[] { m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44 }.All(float.IsFinite);
}
