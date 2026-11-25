using System.Numerics;
using TrueMoon;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Mods;

namespace TrueMoon.ManualTests.ModFixture;

public sealed class ReloadEntry : IModEntryPoint
{
    public static IModEntry Create(IModHostServices services) => ReloadModule.Create(services,
#if MOD_V2
        10
#else
        1
#endif
    );
}
public sealed class SecondReloadEntry : IModEntryPoint
{
    public static IModEntry Create(IModHostServices services) => ReloadModule.Create(services, 10);
}
internal sealed class ReloadModule(Action<string> trace, Func<string, bool> fail, Action<Action> subscribe,
    Action<object> retain, IModGeneration generation, int step) : IModEntry, IModReloadParticipant
{
    private string Prefix => step == 1 ? "v1:" : "v2:";
    internal static IModEntry Create(IModHostServices services, int step) => new ReloadModule(
        services.GetRequired<Action<string>>(), services.GetRequired<Func<string, bool>>(),
        services.GetRequired<Action<Action>>(), services.GetRequired<Action<object>>(), services.GetRequired<IModGeneration>(), step);
    private void Check(string stage) { trace(Prefix + stage); if (fail(Prefix + stage)) throw new ReloadException(); }
    public void Register(IModRegistration registration)
    {
        Check("register");
        var id = fail(Prefix + "different-id") ? "mod.other" : "mod.stable";
        var count = fail(Prefix + "no-systems") ? 0 : fail(Prefix + "many-systems") ? 65 : 1;
        for (var i = 0; i < count; i++)
            registration.AddSystem(new EcsSystemDescription(count == 1 ? id : id + "." + i).Write<LinearVelocity>().Freeze(), new Factory<ISystem>(() =>
            { Check("factory"); return new StableSystem(trace, fail, retain, step); }));
        registration.AddService("mod.service", new Factory<IModService>(() => new ReloadService(trace, fail, Prefix)));
    }
    public ValueTask StartAsync(CancellationToken token)
    {
        Check("start"); subscribe(generation.Guard(() => Check("callback")));
        if (fail(Prefix + "start-wait")) return new(Task.Delay(Timeout.Infinite, token));
        return ValueTask.CompletedTask;
    }
    public ValueTask QuiesceAsync(CancellationToken token)
    {
        Check("quiesce"); if (fail(Prefix + "quiesce-wait")) return new(Task.Delay(Timeout.Infinite, token));
        token.ThrowIfCancellationRequested(); return ValueTask.CompletedTask;
    }
    public ValueTask ResumeAsync(CancellationToken token) { Check("resume"); return ValueTask.CompletedTask; }
    public ValueTask StopAsync(CancellationToken token) { Check("stop"); return ValueTask.CompletedTask; }
    public ValueTask DisposeAsync() { Check("dispose"); return ValueTask.CompletedTask; }
    private sealed class StableSystem(Action<string> trace, Func<string, bool> fail, Action<object> retain, int step) : IModReloadableSystem
    {
        private int _ticks;
        private string Prefix => step == 1 ? "v1:" : "v2:";
        private void Check(string stage) { trace(Prefix + stage); if (fail(Prefix + stage)) throw new ReloadException(); }
        public void Execute(EcsSystemContext context)
        {
            Check("execute"); _ticks++;
            trace(Prefix + "tick:" + context.TickNumber + ":state:" + _ticks);
            retain(context);
            context.Query<LinearVelocity>().ForEachWrite((EntityHandle _, ref LinearVelocity value) => value = new(value.Value + new Vector3(step, 0, 0)));
            if (fail(Prefix + "commands")) context.Commands.CreateEntity();
        }
        public ModSystemState CaptureState()
        {
            Check("capture");
            return new(fail(Prefix + "state-version") ? 2 : 1,
                fail(Prefix + "oversize") ? new byte[ModSystemState.MaximumBytes + 1] :
                fail(Prefix + "large-state") ? new byte[ModSystemState.MaximumBytes] : BitConverter.GetBytes(_ticks));
        }
        public void RestoreState(ModSystemState state)
        {
            Check("restore");
            if (state.Version != 1 || state.Bytes.Length != 4) throw new ReloadException();
            _ticks = BitConverter.ToInt32(state.Bytes.Span);
        }
        public void Stop() => Check("system-stop");
        public void Dispose() => Check("system-dispose");
    }
    private sealed class ReloadService(Action<string> trace, Func<string, bool> fail, string prefix) : IModService, IModReloadParticipant
    {
        private void Check(string stage) { trace(prefix + stage); if (fail(prefix + stage)) throw new ReloadException(); }
        public ValueTask StartAsync(CancellationToken token) { Check("service-start"); return ValueTask.CompletedTask; }
        public ValueTask QuiesceAsync(CancellationToken token) { Check("service-quiesce"); return ValueTask.CompletedTask; }
        public ValueTask ResumeAsync(CancellationToken token) { Check("service-resume"); return ValueTask.CompletedTask; }
        public ValueTask StopAsync(CancellationToken token) { Check("service-stop"); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { Check("service-dispose"); return ValueTask.CompletedTask; }
    }
    private sealed class ReloadException : Exception;
}
