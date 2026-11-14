using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Ecs.Loading;
using TrueMoon.Aether.Abstractions.Mods;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;

namespace TrueMoon.Aether.Runtime.Mods;

internal sealed class ModComponentCatalog
{
    internal readonly Dictionary<ComponentTypeId, Binding> Bindings = [];
    internal readonly Dictionary<(ComponentTypeId, int, int), Rule> Rules = [];
    private readonly HashSet<Type> _types = [];
    internal void Add<T>(ComponentSchema schema, IFactory<T, ModComponentReadArguments> read,
        IFactory<ComponentData, ModComponentWriteArguments<T>> write, IEnumerable<ComponentReference>? references) where T : struct
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(read); ArgumentNullException.ThrowIfNull(write); World.ValidateComponent<T>();
        if (Bindings.Count == 256 || Bindings.ContainsKey(schema.TypeId) || !_types.Add(typeof(T))) throw new ArgumentException("Duplicate/excessive migration binding.");
        var refs = (references ?? []).ToArray();
        if (refs.Any(r => r == null || r.Kind != ComponentReferenceKind.Entity)) throw new NotSupportedException("World migration v1 supports declared entity references; asset preparation is module-owned.");
        Bindings.Add(schema.TypeId, new TypedBinding<T>(schema, read, write, refs));
    }
    internal void AddRule(ComponentTypeId type, int from, int to, IFactory<IModComponentMigrator> factory, IEnumerable<string>? removed)
    {
        ArgumentNullException.ThrowIfNull(factory); var fields = (removed ?? []).ToArray();
        if (type.Value == Guid.Empty || from <= 0 || to <= 0 || from == to || Rules.Count == 256 ||
            fields.Length > 256 || fields.Any(string.IsNullOrWhiteSpace) || fields.Distinct(StringComparer.Ordinal).Count() != fields.Length ||
            !Rules.TryAdd((type, from, to), new(factory, fields))) throw new ArgumentException("Invalid/duplicate migration rule.");
    }
    internal ComponentRegistry Build(Func<ComponentLoadContext, IModEntityMap> map)
    {
        var builder = new ComponentRegistryBuilder(); foreach (var binding in Bindings.Values) binding.Register(builder, map); return builder.Build();
    }
    internal void Clear() { Bindings.Clear(); Rules.Clear(); _types.Clear(); }
    internal sealed record Rule(IFactory<IModComponentMigrator> Factory, string[] Removed);
    internal abstract class Binding(ComponentSchema schema, Type type)
    {
        internal ComponentSchema Schema { get; } = schema;
        internal Type Type { get; } = type;
        internal abstract void Register(ComponentRegistryBuilder builder, Func<ComponentLoadContext, IModEntityMap> map);
        internal abstract ComponentData? Capture(World world, EntityHandle entity, ComponentId id, IModEntityMap map);
        internal static ComponentData Values(ResolvedComponent data) => new(data.Id, data.TypeId, data.SchemaVersion,
            data.Properties.Select(p => new KeyValuePair<string, PropertyValue>(p.Key, p.Value.Value)));
    }
    private sealed class TypedBinding<T>(ComponentSchema schema, IFactory<T, ModComponentReadArguments> read,
        IFactory<ComponentData, ModComponentWriteArguments<T>> write, ComponentReference[] refs) : Binding(schema, typeof(T)) where T : struct
    {
        internal override void Register(ComponentRegistryBuilder builder, Func<ComponentLoadContext, IModEntityMap> map)
            => builder.Register<T>(Schema, context => read.Create(new(Values(context.Component), map(context))), refs);
        internal override ComponentData? Capture(World world, EntityHandle entity, ComponentId id, IModEntityMap map)
        {
            if (!world.TryGetComponent<T>(entity, out var value)) return null;
            var data = write.Create(new(id, value, map)) ?? throw new InvalidDataException("Export factory returned null.");
            if (data.Id != id || data.TypeId != Schema.TypeId || data.SchemaVersion != Schema.Version) throw new InvalidDataException("Export changed component/type/schema identity.");
            if (!Schema.Properties.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(data.Properties.Keys)) throw new InvalidDataException("Export must supply every schema property exactly; implicit defaulting would hide data loss.");
            return data;
        }
    }
    internal static bool SameSchema(ComponentSchema a, ComponentSchema b) => a.TypeId == b.TypeId && a.Version == b.Version && a.AllowMultiple == b.AllowMultiple &&
        a.RequiredTypes.SetEquals(b.RequiredTypes) && a.Properties.Count == b.Properties.Count && a.Properties.All(p => b.Properties.TryGetValue(p.Key, out var q) &&
            p.Value.Kind == q.Kind && p.Value.IsNullable == q.IsNullable && p.Value.DefaultValue.Equals(q.DefaultValue));
}
