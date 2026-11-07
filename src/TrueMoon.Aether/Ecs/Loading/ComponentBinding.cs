using System.Collections.Immutable;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Loading;

internal interface IComponentBinding
{
    ComponentSchema Schema { get; }
    Type RuntimeType { get; }
    ImmutableDictionary<string, ComponentReferenceKind> References { get; }
    void Validate(ComponentValidationContext context);
    IPreparedComponent Prepare(ComponentLoadContext context);
}

internal interface IPreparedComponent
{
    void Apply(World world, object owner);
}

internal sealed class ComponentBinding<T>(ComponentSchema schema, Func<ComponentLoadContext, T> importer,
    ImmutableDictionary<string, ComponentReferenceKind> references, Action<ComponentValidationContext>? validate) : IComponentBinding where T : struct
{
    public ComponentSchema Schema => schema;
    public Type RuntimeType => typeof(T);
    public ImmutableDictionary<string, ComponentReferenceKind> References => references;
    public void Validate(ComponentValidationContext context) => validate?.Invoke(context);
    public IPreparedComponent Prepare(ComponentLoadContext context) => new Prepared(context.Entity, importer(context));
    private sealed class Prepared(EntityHandle entity, T value) : IPreparedComponent
    {
        public void Apply(World world, object owner) => world.AddSceneComponent(owner, entity, value);
    }
}
