using TrueMoon;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Services;

namespace Game.Logic;

// Explicit selected-game consumer: this type is instantiated only in the child entry factory.
public sealed class WorldProbeGame(SceneWorldLoader loader) : SceneWorldGameSession(loader), IGameSessionEntry
{
    public static IGameSession Create(IServiceResolver? services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var schema = new ComponentSchema(new(Guid.Parse("73f4de14-05d4-4654-81e2-f0d4e353ca10")), 1,
        [
            new("value", PropertyValueKind.Integer, PropertyValue.From(3L)),
            new("note", PropertyValueKind.String, PropertyValue.From("from-template")),
            new("target", PropertyValueKind.String, PropertyValue.Null, isNullable: true)
        ]);
        var registry = new ComponentRegistryBuilder().Register<ProbeValue>(schema,
            context => new(context.GetInteger("value"), context.GetString("note")!.Length, context.GetEntityReference("target") ?? default),
            [new("target", ComponentReferenceKind.Entity)]).Build();
        var worlds = services.Resolve<IFactory<SceneWorldLoader, ComponentRegistry>>()!;
        return new WorldProbeGame(worlds.Create(registry));
    }

    public override uint PatternColor
    {
        get
        {
            if (ActiveScene is not { } scene) return 0xff000000;
            long sum = 0;
            scene.World.Query<ProbeValue>().ForEach((EntityHandle entity, in ProbeValue value) =>
            {
                if (value.Target != default && !scene.World.IsAlive(value.Target)) throw new InvalidDataException("Lost same-World reference.");
                sum = checked(sum + value.Value + value.NoteLength + (value.Target == default ? 0 : 17));
            });
            return 0xff000000u | (uint)(sum & 0xffffff);
        }
    }
    protected override void TickWorld(SceneWorld scene, ulong nextTick)
        => scene.World.Query<ProbeValue>().ForEachWrite(static (EntityHandle entity, ref ProbeValue value) => value.Value = checked(value.Value + 1));

    private struct ProbeValue(long value, int noteLength, EntityHandle target)
    {
        public long Value = value;
        public int NoteLength = noteLength;
        public EntityHandle Target = target;
    }
}
