using System.Collections.Immutable;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Loading;

/// <summary>Immutable, non-global runtime bindings. It borrows importers/validators, never their dependencies' ownership.</summary>
public sealed class ComponentRegistry
{
    private readonly ImmutableDictionary<(ComponentTypeId, int), IComponentBinding> _bindings;
    internal ComponentRegistry(ImmutableDictionary<(ComponentTypeId, int), IComponentBinding> bindings, Action<Guid, CancellationToken>? assets)
    { _bindings = bindings; AssetValidator = assets; Schemas = [.. bindings.Values.Select(b => b.Schema).OrderBy(s => s.TypeId.Value).ThenBy(s => s.Version)]; }
    /// <summary>Gets the runtime registry identity, independent of persistent type IDs.</summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>Gets exact binding count, including multiple registered versions of the same type.</summary>
    public int Count => _bindings.Count;
    /// <summary>Gets neutral schema metadata in stable ID/version order.</summary>
    public ImmutableArray<ComponentSchema> Schemas { get; }
    internal Action<Guid, CancellationToken>? AssetValidator { get; }
    internal IComponentBinding Binding(ComponentSchema actual, SceneId scene)
    {
        if (!_bindings.TryGetValue((actual.TypeId, actual.Version), out var binding))
        {
            var error = _bindings.Keys.Any(k => k.Item1 == actual.TypeId) ? SceneWorldLoadError.UnsupportedSchemaVersion : SceneWorldLoadError.UnknownComponent;
            throw new SceneWorldLoadException(error, $"No exact runtime binding for {actual.TypeId.Value:D} v{actual.Version}.", scene);
        }
        if (actual.AllowMultiple) throw new SceneWorldLoadException(SceneWorldLoadError.UnsupportedMultiplicity, "Multiple component occurrences are unsupported.", scene);
        var expected = binding.Schema;
        if (!actual.RequiredTypes.SetEquals(expected.RequiredTypes) || actual.Properties.Count != expected.Properties.Count ||
            actual.Properties.Any(pair => !expected.Properties.TryGetValue(pair.Key, out var value) ||
                value.Kind != pair.Value.Kind || value.IsNullable != pair.Value.IsNullable || value.DefaultValue != pair.Value.DefaultValue))
            throw new SceneWorldLoadException(SceneWorldLoadError.SchemaMismatch, $"Schema drift under exact type/version {actual.TypeId.Value:D} v{actual.Version}.", scene);
        return binding;
    }
}
