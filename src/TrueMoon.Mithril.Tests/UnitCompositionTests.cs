using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Mithril.Units;
using TrueMoon.Services;

namespace TrueMoon.Mithril.Tests;

public class UnitCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedBuilds_HaveIndependentUnitIndexesNamesAndActions(bool di)
    {
        var builder = Builder(di).Setup(c => c.AddUnit(_ => { }, u => { u.Name = "first"; u.StartDelayed(); u.RestartPolicy = UnitRestartPolicy.Never; }));
        await using var first = builder.Build();
        await using var second = builder.Build();
        await using var other = Builder(di).Setup(c => c.AddUnit(_ => { }, u => { u.Name = "other"; u.StartDelayed(); u.RestartPolicy = UnitRestartPolicy.Never; })).Build();
        var a = first.Services.Resolve<IReadOnlyList<IUnitConfiguration>>()!;
        var b = second.Services.Resolve<IReadOnlyList<IUnitConfiguration>>()!;
        var d = other.Services.Resolve<IReadOnlyList<IUnitConfiguration>>()!;
        Assert.Equal((1, "first"), (Assert.Single(a).Index, a[0].Name));
        Assert.Equal((1, "first"), (Assert.Single(b).Index, b[0].Name));
        Assert.Equal((1, "other"), (Assert.Single(d).Index, d[0].Name));
        Assert.NotSame(a[0], b[0]);
        a[0].Name = "mutated";
        Assert.Equal("first", b[0].Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MainUnit_UsesCurrentAppGraphAndRunsLocalLifecycleOnce(bool di)
    {
        await using var app = Builder(di).Setup(c => c.AddUnit(unit => unit
            .Configuration(config => config.Set("local-test", 42))
            .Services(r => r.Composite<LocalLifecycle, IStartable, IStoppable>()), u => u.ExecuteInMainProcess())).Build();
        Assert.Equal(42, app.Configuration.Get<int>("local-test"));
        var service = Assert.Single(app.Services.ResolveAll<IStartable>().OfType<LocalLifecycle>());
        await app.StartAsync(); await app.StartAsync();
        Assert.Equal(1, service.Starts); Assert.Equal(Environment.ProcessId, service.Pid);
        await app.StopAsync(); await app.StopAsync();
        Assert.Equal(1, service.Stops);
        Assert.False(app.Services.Resolve<IAppLifetime>()!.AppCancellationToken.IsCancellationRequested);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChildSelection_AppliesOnlySelectedUnitAndItsConfiguration(bool di)
    {
        var first = 0; var second = 0;
        await using var app = Builder(di).Setup(c => c.Configuration(config => config.Set("-u", "1"))
            .AddUnit(unit => { first++; unit.Configuration(config => config.Set("selected", "first")); })
            .AddUnit(_ => second++)).Build();
        Assert.Equal(1, first); Assert.Equal(0, second);
        Assert.Equal("first", app.Configuration.Get<string>("selected"));
        Assert.Null(app.Services.GetService(typeof(IUnitsController)));
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("0")]
    [InlineData("2")]
    public void InvalidOrUnknownChildSelection_FailsBeforeUnitAction(string id)
    {
        var calls = 0;
        Assert.ThrowsAny<ArgumentException>(() => Builder(true).Setup(c => c.Configuration(config => config.Set("-u", id))
            .AddUnit(_ => calls++)).Build());
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InjectedHandleFactory_UsesSnapshotsAndControllerOwnsEveryResult(bool di, bool failSecond)
    {
        var handles = new List<OwnedHandle>(); var log = new List<string>();
        var expected = new IOException("second injected handle failed");
        var app = Builder(di).Setup(c =>
        {
            c.Services(services => services.Singleton<IFactory<IUnitHandle, IUnitConfiguration>>(_ =>
                new Factory<IUnitHandle, IUnitConfiguration>(configuration =>
                {
                    var handle = new OwnedHandle(configuration, log, failSecond && configuration.Index == 2 ? expected : null);
                    handles.Add(handle); return handle;
                })));
            c.AddUnit(_ => { }, unit => { unit.Name = "first"; unit.RestartPolicy = UnitRestartPolicy.Never; });
            c.AddUnit(_ => { }, unit => { unit.Name = "second"; unit.RestartPolicy = UnitRestartPolicy.Never; });
        }).Build();
        var controller = app.Services.Resolve<IUnitsController>()!;
        app.Services.Resolve<IReadOnlyList<IUnitConfiguration>>()![0].Name = "mutated";
        Assert.Empty(handles);
        if (failSecond) Assert.Same(expected, await Assert.ThrowsAsync<IOException>(() => app.StartAsync()));
        else
        {
            await app.StartAsync();
            Assert.All(handles, handle => { Assert.Equal(1, handle.Starts); Assert.Equal(0, handle.Stops); Assert.Equal(0, handle.Disposals); });
            await app.StopAsync();
        }
        Assert.Collection(handles,
            handle => Assert.Equal((1, "first"), (handle.GetConfiguration().Index, handle.GetConfiguration().Name)),
            handle => Assert.Equal((2, "second"), (handle.GetConfiguration().Index, handle.GetConfiguration().Name)));
        Assert.NotSame(handles[0], handles[1]);
        Assert.Equal(new[] { "start1", "start2", "stop2", "dispose2", "stop1", "dispose1" }, log);
        Assert.All(handles, handle => { Assert.Equal(1, handle.Starts); Assert.Equal(1, handle.Stops); Assert.Equal(1, handle.Disposals); });
        await app.DisposeAsync(); await app.DisposeAsync();
        Assert.All(handles, handle => Assert.Equal(1, handle.Disposals));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => controller.SpawnUnitAsync(1));
    }

    private sealed class OwnedHandle(IUnitConfiguration configuration, List<string> log, Exception? failure)
        : UnitHandleBase(configuration, new TestEvents<OwnedHandle>()), IAsyncDisposable
    {
        public int Starts, Stops, Disposals;
        public override Task<bool?> StartAsync(CancellationToken cancellationToken = default)
        {
            Starts++; log.Add("start" + Configuration.Index);
            return failure == null ? Task.FromResult<bool?>(true) : Task.FromException<bool?>(failure);
        }
        public override Task<bool?> StopAsync(CancellationToken cancellationToken = default)
        { Stops++; log.Add("stop" + Configuration.Index); return Task.FromResult<bool?>(true); }
        public ValueTask DisposeAsync()
        { Disposals++; log.Add("dispose" + Configuration.Index); return ValueTask.CompletedTask; }
    }

    private static IAppBuilder Builder(bool di) => di ? App.Builder(c => c.UseDI()) : App.Builder(_ => { });
    public sealed class LocalLifecycle : IStartable, IStoppable
    {
        public int Starts, Stops, Pid;
        public Task StartAsync(CancellationToken token = default) { Starts++; Pid = Environment.ProcessId; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken token = default) { Stops++; return Task.CompletedTask; }
    }
}
