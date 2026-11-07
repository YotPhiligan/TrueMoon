using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Loading;

/// <summary>Data-only callback scope. It cannot expose mutable World or acquire resources.</summary>
public class ComponentValidationContext
{
    private readonly ResolvedSceneSnapshot _scene;
    private readonly ResolvedEntity _entity;
    private readonly ResolvedComponent _component;
    private readonly IComponentBinding _binding;
    private readonly CancellationToken _token;
    private bool _active = true;
    internal ComponentValidationContext(ResolvedSceneSnapshot scene, ResolvedEntity entity, ResolvedComponent component,
        IComponentBinding binding, CancellationToken token)
    { _scene = scene; _entity = entity; _component = component; _binding = binding; _token = token; }
    /// <summary>Gets immutable complete resolved authoring metadata.</summary>
    public ResolvedSceneSnapshot Scene { get { CheckActive(); return _scene; } }
    /// <summary>Gets the immutable source entity and hierarchy metadata.</summary>
    public ResolvedEntity SourceEntity { get { CheckActive(); return _entity; } }
    /// <summary>Gets effective values and provenance, including defaults and explicit null.</summary>
    public ResolvedComponent Component { get { CheckActive(); return _component; } }
    /// <summary>Gets the exact registered schema.</summary>
    public ComponentSchema Schema { get { CheckActive(); return _binding.Schema; } }
    /// <summary>Gets cooperative cancellation.</summary>
    public CancellationToken CancellationToken { get { CheckActive(); return _token; } }
    /// <summary>Reads an effective value without coercion.</summary><param name="propertyId">Ordinal key.</param><returns>Effective scalar.</returns>
    public PropertyValue GetValue(string propertyId)
    {
        CheckActive();
        if (propertyId == null || !_component.Properties.TryGetValue(propertyId, out var property))
            throw Error(SceneWorldLoadError.InvalidValue, "Unknown property.", propertyId);
        return property.Value;
    }
    /// <summary>Reads an exact Boolean.</summary><param name="propertyId">Property key.</param><returns>Boolean value.</returns>
    public bool GetBoolean(string propertyId) => Read(propertyId, static v => v.GetBoolean());
    /// <summary>Reads an exact Int64.</summary><param name="propertyId">Property key.</param><returns>Integer value.</returns>
    public long GetInteger(string propertyId) => Read(propertyId, static v => v.GetInteger());
    /// <summary>Reads an exact Number without implicit integer conversion.</summary><param name="propertyId">Property key.</param><returns>Finite double.</returns>
    public double GetNumber(string propertyId)
    {
        var value = Read(propertyId, static v => v.GetNumber());
        if (!double.IsFinite(value)) throw Error(SceneWorldLoadError.InvalidValue, "Nonfinite number.", propertyId);
        return value;
    }
    /// <summary>Reads an exact nonnull string. Use GetValue for explicit nullable handling.</summary><param name="propertyId">Property key.</param><returns>String value.</returns>
    public string GetString(string propertyId) => Read(propertyId, static v => v.GetString());
    /// <summary>Converts an exact Integer to Int32 with inclusive bounds, without truncation.</summary>
    /// <param name="propertyId">Property key.</param><param name="minimum">Inclusive minimum.</param><param name="maximum">Inclusive maximum.</param><returns>Validated integer.</returns>
    public int GetInt32(string propertyId, int minimum = int.MinValue, int maximum = int.MaxValue)
    {
        var value = GetInteger(propertyId);
        if (minimum > maximum || value < minimum || value > maximum) throw Error(SceneWorldLoadError.InvalidValue, "Integer conversion/range failure.", propertyId);
        return (int)value;
    }
    /// <summary>Converts an exact Number to finite Single with inclusive bounds.</summary>
    /// <param name="propertyId">Property key.</param><param name="minimum">Inclusive minimum.</param><param name="maximum">Inclusive maximum.</param><returns>Validated finite float.</returns>
    public float GetSingle(string propertyId, float minimum = float.NegativeInfinity, float maximum = float.PositiveInfinity)
    {
        var value = GetNumber(propertyId); var converted = (float)value;
        if (!float.IsFinite(converted) || value < -float.MaxValue || value > float.MaxValue ||
            float.IsNaN(minimum) || float.IsNaN(maximum) || minimum > maximum || value < minimum || value > maximum)
            throw Error(SceneWorldLoadError.InvalidValue, "Number conversion/range failure.", propertyId);
        return converted;
    }
    /// <summary>Resolves declared entity reference metadata without runtime handles.</summary>
    /// <param name="propertyId">Declared entity reference key.</param><returns>Target metadata, or null for explicit null.</returns>
    public ResolvedEntity? GetEntityData(string propertyId)
    { var id = ReferenceId(propertyId, ComponentReferenceKind.Entity); return id is { } value ? _scene.Entities[new(value)] : null; }
    private protected Guid? ReferenceId(string propertyId, ComponentReferenceKind kind)
    {
        CheckActive();
        if (!_binding.References.TryGetValue(propertyId, out var actual) || actual != kind)
            throw Error(SceneWorldLoadError.InvalidReference, "Reference semantics were not declared for this property.", propertyId);
        var value = GetValue(propertyId);
        if (value.Kind == PropertyValueKind.Null) return null;
        if (value.Kind != PropertyValueKind.String || value.GetString().Length != 36 || !Guid.TryParseExact(value.GetString(), "D", out var id) || id == Guid.Empty)
            throw Error(SceneWorldLoadError.InvalidReference, "References require a nonempty GUID in D format.", propertyId);
        return id;
    }
    private protected void CheckActive() { if (!_active) throw new InvalidOperationException("The component callback context is no longer active."); }
    internal void Deactivate() => _active = false;
    private protected SceneWorldLoadException Error(SceneWorldLoadError error, string message, string? property = null, Exception? inner = null)
        => new(error, message, _scene.Id, _entity.Id, _component.Id, property, inner);
    private T Read<T>(string property, Func<PropertyValue, T> read)
    {
        var value = GetValue(property);
        try { return read(value); }
        catch (InvalidOperationException error) { throw Error(SceneWorldLoadError.InvalidValue, "Property kind mismatch or explicit null.", property, error); }
    }
}
