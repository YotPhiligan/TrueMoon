using TrueMoon;
using TrueMoon.Aether.Abstractions.Ecs;
using TrueMoon.Aether.Abstractions.Ecs.Loading;
using TrueMoon.Aether.Abstractions.Mods;
using TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.ManualTests.ModFixture;

public sealed class MigrationEntry : IModEntryPoint
{
    public static IModEntry Create(IModHostServices services) => MigrationModule.Create(services,
#if MOD_V2
        true
#else
        false
#endif
    );
}
public sealed class SecondMigrationEntry : IModEntryPoint
{ public static IModEntry Create(IModHostServices services) => MigrationModule.Create(services, true); }
internal sealed class MigrationModule(Action<string> trace, Func<string, bool> fail, Action<Action> subscribe,
    Action<object> retain, IModGeneration generation, bool second, ISystem? sharedSystem) : IModEntry, IModReloadParticipant
{
    private static readonly ComponentTypeId CounterId = new(Guid.Parse("38000000-0000-0000-0000-000000000001"));
    private static readonly ComponentTypeId LinkId = new(Guid.Parse("38000000-0000-0000-0000-000000000002"));
    private string Prefix => second ? "m2:" : "m1:";
    internal static IModEntry Create(IModHostServices services, bool second)
    {
        var fail = services.GetRequired<Func<string, bool>>();
        return new MigrationModule(services.GetRequired<Action<string>>(), fail, services.GetRequired<Action<Action>>(),
            services.GetRequired<Action<object>>(), services.GetRequired<IModGeneration>(), second,
            fail((second ? "m2:" : "m1:") + "shared-system") ? services.GetRequired<IFactory<ISystem>>().Create() : null);
    }
    private void Check(string stage) { trace(Prefix + stage); if (fail(Prefix + stage)) throw new MigrationException(); }
    private void Trace(string text) => trace(text);
    private bool Fails(string text) => fail(Prefix + text);
    private static ComponentPropertySchema Integer(string id) => new(id, PropertyValueKind.Integer, PropertyValue.From(0L));
    private static ComponentData Data(ComponentId id, ComponentTypeId type, int version, params (string, PropertyValue)[] properties)
        => new(id, type, version, properties.Select(p => new KeyValuePair<string, PropertyValue>(p.Item1, p.Item2)));
    public void Register(IModRegistration registration)
    {
        Check("register"); var drop = fail(Prefix + "drop-field");
        if (!fail(Prefix + "unknown-type"))
        {
            if (!second)
                registration.AddMigratingComponent(new(CounterId, 1, [Integer("value"), Integer("obsolete")]),
                    new Factory<Counter1, ModComponentReadArguments>(a => { Check("import"); return new(a.Data.Properties["value"].GetInteger(), a.Data.Properties["obsolete"].GetInteger()); }),
                    new Factory<ComponentData, ModComponentWriteArguments<Counter1>>(a => { Check("export"); return Data(a.Id, CounterId, 1, ("value", PropertyValue.From(a.Value.Value)), ("obsolete", PropertyValue.From(a.Value.Obsolete))); }));
            else
            {
                var properties = new List<ComponentPropertySchema> { new("value", PropertyValueKind.Number, PropertyValue.From(0.0)), Integer("bonus") };
                if (!drop) properties.Add(Integer("obsolete"));
                registration.AddMigratingComponent(new(CounterId, fail(Prefix + "same-version") ? 1 : 2, properties),
                    new Factory<Counter2, ModComponentReadArguments>(a => { Check("import"); return new(a.Data.Properties["value"].GetNumber(), a.Data.Properties["bonus"].GetInteger(), drop ? 0 : a.Data.Properties["obsolete"].GetInteger()); }),
                    new Factory<ComponentData, ModComponentWriteArguments<Counter2>>(a => { Check("export"); var p = new List<(string, PropertyValue)> { ("value", PropertyValue.From(a.Value.Total)), ("bonus", PropertyValue.From(a.Value.Bonus)) }; if (!drop) p.Add(("obsolete", PropertyValue.From(a.Value.Obsolete))); return Data(a.Id, CounterId, 2, p.ToArray()); }));
                if (!fail(Prefix + "no-migrator")) registration.AddComponentMigration(CounterId, 1, 2,
                    new Factory<IModComponentMigrator>(() => { Check("converter-factory"); return new Converter(this, drop); }), drop && !fail(Prefix + "undeclared-removal") ? ["obsolete"] : []);
            }
        }
        registration.AddMigratingComponent(new(LinkId, 1, [new("target", PropertyValueKind.String, PropertyValue.Null, true)]),
            new Factory<Link, ModComponentReadArguments>(a => new(a.Entities.Resolve(new(Guid.Parse(a.Data.Properties["target"].GetString()))))),
            new Factory<ComponentData, ModComponentWriteArguments<Link>>(a => Data(a.Id, LinkId, 1, ("target", PropertyValue.From(a.Entities.Persistent(a.Value.Target).Value.ToString("D"))))),
            [new("target", ComponentReferenceKind.Entity)]);
        if (second) registration.AddSystem(new EcsSystemDescription("mod.migration").Write<Counter2>().Read<Link>().Freeze(),
            new Factory<ISystem>(() => { Check("factory"); return sharedSystem ?? new System2(this); }));
        else registration.AddSystem(new EcsSystemDescription("mod.migration").Write<Counter1>().Read<Link>().Freeze(),
            new Factory<ISystem>(() => { Check("factory"); return sharedSystem ?? new System1(this); }));
    }
    public ValueTask StartAsync(CancellationToken token) { Check("start"); subscribe(generation.Guard(() => Check("callback"))); return fail(Prefix + "start-wait") ? new(Task.Delay(Timeout.Infinite, token)) : ValueTask.CompletedTask; }
    public ValueTask QuiesceAsync(CancellationToken token) { Check("quiesce"); return fail(Prefix + "quiesce-wait") ? new(Task.Delay(Timeout.Infinite, token)) : ValueTask.CompletedTask; }
    public ValueTask ResumeAsync(CancellationToken token) { Check("resume"); return ValueTask.CompletedTask; }
    public ValueTask StopAsync(CancellationToken token) { Check("stop"); return ValueTask.CompletedTask; }
    public ValueTask DisposeAsync() { Check("dispose"); return ValueTask.CompletedTask; }
    private readonly record struct Counter1(long Value, long Obsolete);
    private readonly record struct Counter2(double Total, long Bonus, long Obsolete);
    private readonly record struct Link(EntityHandle Target);
    private abstract class SystemBase(MigrationModule owner) : IModReloadableSystem
    {
        protected readonly MigrationModule Owner = owner;
        private int _ticks;
        protected void Tick(EcsSystemContext context) { Owner.Check("execute"); _ticks++; Owner.Trace(Owner.Prefix + "tick:" + context.TickNumber + ":state:" + _ticks); Owner.Retain(context); }
        public abstract void Execute(EcsSystemContext context);
        public ModSystemState CaptureState() { Owner.Check("capture"); return new(1, BitConverter.GetBytes(_ticks)); }
        public void RestoreState(ModSystemState state) { Owner.Check("restore"); if (state.Version != 1 || state.Bytes.Length != 4) throw new MigrationException(); _ticks = BitConverter.ToInt32(state.Bytes.Span); }
        public void Stop() => Owner.Check("system-stop");
        public void Dispose() => Owner.Check("system-dispose");
    }
    private sealed class System1(MigrationModule owner) : SystemBase(owner)
    {
        public override void Execute(EcsSystemContext context) { Tick(context); context.Query<Counter1>().ForEachWrite((EntityHandle _, ref Counter1 c) => c = c with { Value = c.Value + 1 }); }
    }
    private sealed class System2(MigrationModule owner) : SystemBase(owner)
    {
        public override void Execute(EcsSystemContext context) { Tick(context); context.Query<Counter2>().ForEachWrite((EntityHandle _, ref Counter2 c) => c = c with { Total = c.Total + 10 }); }
    }
    private sealed class Converter(MigrationModule owner, bool drop) : IModComponentMigrator
    {
        public ComponentData Migrate(ModComponentMigrationArguments a)
        {
            owner.Check("migrate"); var properties = new List<(string, PropertyValue)> { ("value", PropertyValue.From((double)a.Data.Properties["value"].GetInteger())), ("bonus", PropertyValue.From(42L)) };
            if (!drop) properties.Add(("obsolete", a.Data.Properties["obsolete"]));
            return Data(owner.Fails("bad-identity") ? new(Guid.NewGuid()) : a.Data.Id, a.Data.TypeId, 2, properties.ToArray());
        }
        public void Dispose() => owner.Check("converter-dispose");
    }
    private sealed class MigrationException : Exception;
    private void Retain(object value) => retain(value);
}
public sealed class DependencyMigrationEntry : IModEntryPoint
{
    public static IModEntry Create(IModHostServices services) => new Dependency(services.GetRequired<Action<string>>());
    private sealed class Dependency(Action<string> trace) : IModEntry, IModReloadParticipant
    {
        public void Register(IModRegistration registration) => trace("dep:register");
        public ValueTask StartAsync(CancellationToken token) { trace("dep:start"); return ValueTask.CompletedTask; }
        public ValueTask QuiesceAsync(CancellationToken token) { trace("dep:quiesce"); return ValueTask.CompletedTask; }
        public ValueTask ResumeAsync(CancellationToken token) { trace("dep:resume"); return ValueTask.CompletedTask; }
        public ValueTask StopAsync(CancellationToken token) { trace("dep:stop"); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { trace("dep:dispose"); return ValueTask.CompletedTask; }
    }
}
