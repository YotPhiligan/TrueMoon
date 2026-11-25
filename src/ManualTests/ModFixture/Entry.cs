using TrueMoon;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Mods;
using TrueMoon.Aether.Scenes;
using TrueMoon.ManualTests.ModPrivateDependency;

namespace TrueMoon.ManualTests.ModFixture;

// Same source is independently compiled against SDK packages by ModConsumer/Verify.ps1.
public sealed class Entry : IModEntryPoint
{
    public static IModEntry Create(IModHostServices services)
    {
        var trace = services.GetRequired<Action<string>>();
        var fail = services.GetRequired<Func<string, bool>>();
        if (fail("private-resolution")) services.GetRequired<IFactory<CounterSystem>>();
        if (fail("provider-resolution")) services.GetRequired<IServiceProvider>();
        if (fail("app-resolution")) services.GetRequired<IApp>();
        return new Module(trace, fail, services);
    }
    private sealed class Module(Action<string> trace, Func<string, bool> fail, IModHostServices composition) : IModEntry
    {
        public void Register(IModRegistration registration)
        {
            var schema = new ComponentSchema(new(new Guid("f89ee66e-2568-4542-8797-874790d173a9")), 1,
                [new ComponentPropertySchema("value", PropertyValueKind.Integer, PropertyValue.From(0L))]);
            registration.AddComponent(schema, new Factory<Counter, ComponentData>(data => new(checked((int)data.Properties["value"].GetInteger()))));
            registration.AddSystem(new EcsSystemDescription("mod.counter").Write<Counter>().Freeze(),
                new Factory<ISystem>(() => fail("system-create") ? throw new ModException() : new CounterSystem(trace, fail)));
            registration.AddService("mod.service", new Factory<IModService>(() => new Service(trace, fail)));
            trace("root:register");
        }
        public ValueTask StartAsync(CancellationToken token)
        {
            trace("root:start");
            if (fail("late-resolution")) composition.GetRequired<Action<string>>();
            if (fail("startup")) throw new ModException();
            if (fail("cancel-start")) return new(Task.Delay(Timeout.Infinite, token));
            token.ThrowIfCancellationRequested(); return ValueTask.CompletedTask;
        }
        public ValueTask StopAsync(CancellationToken token) { trace("root:stop"); if (fail("stop")) throw new ModException(); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { trace("root:dispose"); if (fail("entry-cleanup")) throw new ModException(); return ValueTask.CompletedTask; }
    }
    private readonly record struct Counter(int Value) : IComponent;
    private sealed class CounterSystem(Action<string> trace, Func<string, bool> fail) : ISystem
    {
        public void Execute(EcsSystemContext context)
        {
            if (fail("execute")) throw new ModException();
            context.Query<Counter>().ForEachWrite((EntityHandle _, ref Counter value) => { trace("execute:" + value.Value); value = new(ModMath.Next(value.Value)); });
        }
        public void Stop() => trace("system:stop");
        public void Dispose() { trace("system:dispose"); if (fail("system-cleanup")) throw new ModException(); }
    }
    private sealed class Service(Action<string> trace, Func<string, bool> fail) : IModService
    {
        public ValueTask StartAsync(CancellationToken token) { trace("service:start"); if (fail("service-start")) throw new ModException(); return ValueTask.CompletedTask; }
        public ValueTask StopAsync(CancellationToken token) { trace("service:stop"); if (fail("service-stop")) throw new ModException(); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { trace("service:dispose"); if (fail("service-cleanup")) throw new ModException(); return ValueTask.CompletedTask; }
    }
    private sealed class ModException : Exception;
}

public sealed class DependencyEntry : IModEntryPoint
{
    public static IModEntry Create(IModHostServices services) => new Dependency(services.GetRequired<Action<string>>());
    private sealed class Dependency(Action<string> trace) : IModEntry
    {
        public void Register(IModRegistration registration) => trace("dependency:register");
        public ValueTask StartAsync(CancellationToken token) { trace("dependency:start"); return ValueTask.CompletedTask; }
        public ValueTask StopAsync(CancellationToken token) { trace("dependency:stop"); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { trace("dependency:dispose"); return ValueTask.CompletedTask; }
    }
}
