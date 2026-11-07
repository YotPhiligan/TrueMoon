using System.Collections.Immutable;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Loading;

/// <summary>Composition-time exact schema bindings; Build freezes metadata and delegate references.</summary>
public sealed class ComponentRegistryBuilder
{
    private readonly Dictionary<(ComponentTypeId, int), IComponentBinding> _bindings = [];
    private readonly Dictionary<ComponentTypeId, Type> _types = [];
    private readonly Dictionary<Type, ComponentTypeId> _ids = [];
    private Action<Guid, CancellationToken>? _assetValidator;
    /// <summary>Registers one exact schema version. Singleton value storage does not support AllowMultiple.</summary>
    /// <typeparam name="T">Runtime value type without managed references.</typeparam><param name="schema">Immutable exact schema.</param>
    /// <param name="importer">Borrowed conversion/preparation callback, invoked only during loading.</param>
    /// <param name="references">Optional string reference declarations, copied immediately.</param>
    /// <param name="validate">Optional data-only validation before World creation.</param><returns>This builder.</returns>
    public ComponentRegistryBuilder Register<T>(ComponentSchema schema, Func<ComponentLoadContext, T> importer,
        IEnumerable<ComponentReference>? references = null, Action<ComponentValidationContext>? validate = null) where T : struct
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(importer); World.ValidateComponent<T>();
        if (schema.AllowMultiple) throw new NotSupportedException("The first World load profile supports singleton schemas only.");
        if (_bindings.ContainsKey((schema.TypeId, schema.Version))) throw new ArgumentException("Duplicate exact component binding.", nameof(schema));
        if (_bindings.Count == 1024) throw new InvalidOperationException("Registry binding budget exhausted.");
        if (_types.TryGetValue(schema.TypeId, out var type) && type != typeof(T) ||
            _ids.TryGetValue(typeof(T), out var id) && id != schema.TypeId)
            throw new ArgumentException("Stable type IDs and runtime types must have an unambiguous mapping.", nameof(schema));
        var refs = ImmutableDictionary.CreateBuilder<string, ComponentReferenceKind>(StringComparer.Ordinal);
        foreach (var reference in references ?? [])
        {
            ArgumentNullException.ThrowIfNull(reference);
            if (!schema.Properties.TryGetValue(reference.PropertyId, out var property) || property.Kind != PropertyValueKind.String)
                throw new ArgumentException("References require a declared String property.", nameof(references));
            if (!refs.TryAdd(reference.PropertyId, reference.Kind)) throw new ArgumentException("Duplicate reference declaration.", nameof(references));
        }
        _bindings.Add((schema.TypeId, schema.Version), new ComponentBinding<T>(schema, importer, refs.ToImmutable(), validate));
        _types[schema.TypeId] = typeof(T); _ids[typeof(T)] = schema.TypeId;
        return this;
    }
    /// <summary>Sets a borrowed validator for unique nonnull asset IDs. It must not acquire owned resources.</summary>
    /// <param name="validate">Validation callback with cooperative cancellation.</param><returns>This builder.</returns>
    public ComponentRegistryBuilder WithAssetValidator(Action<Guid, CancellationToken> validate)
    { ArgumentNullException.ThrowIfNull(validate); _assetValidator = validate; return this; }
    /// <summary>Freezes a new independent registry; later builder changes do not alter it.</summary><returns>Immutable runtime registry.</returns>
    public ComponentRegistry Build() => new(_bindings.ToImmutableDictionary(), _assetValidator);
}
