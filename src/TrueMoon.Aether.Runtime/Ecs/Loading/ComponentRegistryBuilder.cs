using System.Collections.Immutable;
using TrueMoon.Aether.Abstractions.Ecs.Loading;
using TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.Aether.Runtime.Ecs.Loading;

/// <summary>Composition-time exact schema bindings; Build freezes metadata and delegate references.</summary>
public sealed class ComponentRegistryBuilder
{
    private readonly Dictionary<(ComponentTypeId, int), IComponentBinding> _bindings = [];
    private readonly Dictionary<ComponentTypeId, Type> _types = [];
    private readonly Dictionary<Type, ComponentTypeId> _ids = [];
    private Action<Guid, CancellationToken>? _assetValidator;
    /// <summary>Registers a neutral scalar value factory by exact schema, using the existing loader and typed pools.</summary>
    /// <typeparam name="T">Reference-free component, with or without IComponent marker.</typeparam>
    /// <param name="schema">Neutral immutable schema. Multiplicity remains unsupported.</param>
    /// <param name="factory">Borrowed DI factory receiving detached effective values, including defaults.</param><returns>This builder.</returns>
    /// <remarks>This data-only path provides no resource acquisition or entity/asset reference resolution. Use the explicit runtime importer for those capabilities.</remarks>
    public ComponentRegistryBuilder RegisterValues<T>(Abstractions.Scenes.ComponentSchema schema,
        IFactory<T, Abstractions.Scenes.ComponentData> factory) where T : struct
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(factory);
        return Register<T>(ContractComponentValues.Legacy(schema), context =>
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var component = context.Component;
            var values = new Abstractions.Scenes.ComponentData(new(component.Id.Value), new(component.TypeId.Value), component.SchemaVersion,
                component.Properties.Select(pair => new KeyValuePair<string, Abstractions.Scenes.PropertyValue>(pair.Key, ContractComponentValues.Neutral(pair.Value.Value))));
            var value = factory.Create(values); context.CancellationToken.ThrowIfCancellationRequested(); return value;
        });
    }
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
