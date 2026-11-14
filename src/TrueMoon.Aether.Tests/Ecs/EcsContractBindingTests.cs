using System.Runtime.CompilerServices;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using A = TrueMoon.Aether.Abstractions.Ecs;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs;

public sealed class EcsContractBindingTests
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(.02);
    private readonly record struct Counter(int Value) : A.IComponent;
    private readonly record struct Velocity(int Value);
    private readonly record struct Disabled;
    private readonly record struct Other;
    private sealed class SystemProbe(Action<A.EcsSystemContext>? execute = null) : A.IEcsSystem
    {
        public int Executions, Stops, Disposals, DisposeFailures;
        public void Execute(A.EcsSystemContext context) { Executions++; execute?.Invoke(context); }
        public void Stop() => Stops++;
        public void Dispose() { Disposals++; if (DisposeFailures-- > 0) throw new IOException("cleanup marker"); }
    }
    private static EcsSystemRegistration Register(A.EcsSystemDescription description, A.ISystem system)
        => new(description, new Factory<A.ISystem>(() => system));
    [Fact]
    public void RealTypedPools_AllCallbackShapesAndExclusions_MutateWithoutComponentBoxing()
    {
        using var world = new World(); var first = world.CreateEntity(); var excluded = world.CreateEntity();
        world.AddComponent(first, new Counter(1)); world.AddComponent(first, new Velocity(2));
        world.AddComponent(excluded, new Counter(100)); world.AddComponent(excluded, new Velocity(20)); world.AddComponent(excluded, new Disabled());
        var callbacks = 0;
        var system = new SystemProbe(context =>
        {
            Assert.Equal(world.Id, context.WorldId); Assert.Equal(1UL, context.TickNumber); Assert.Equal(Step, context.DeltaTime);
            var handle = new A.EntityHandle(world.Id, first.Index, first.Generation);
            Assert.True(context.IsAlive(handle)); Assert.False(context.IsAlive(default)); Assert.True(context.HasComponent<Counter>(handle));
            Assert.True(context.TryGetComponent<Counter>(handle, out var initial)); Assert.Equal(1, initial.Value);
            var query = context.Query<Counter>().Without<Disabled>();
            query.ForEach((A.EntityHandle e, in Counter c) => { Assert.Equal(handle, e); Assert.Equal(1, c.Value); callbacks++; });
            query.ForEachWrite((A.EntityHandle e, ref Counter c) => { c = new(c.Value + 1); callbacks++; });
            var pair = context.Query<Counter, Velocity>().Without<Disabled>();
            pair.ForEach((A.EntityHandle e, in Counter c, in Velocity v) => { Assert.Equal((2, 2), (c.Value, v.Value)); callbacks++; });
            pair.ForEachWrite((A.EntityHandle e, ref Counter c, in Velocity v) => { c = new(c.Value + v.Value); callbacks++; });
            pair.ForEachWriteBoth((A.EntityHandle e, ref Counter c, ref Velocity v) => { c = new(c.Value + v.Value); v = new(v.Value + 1); callbacks++; });
        });
        using (var scheduler = EcsScheduler.Create(world, [Register(new A.EcsSystemDescription("typed").Write<Counter>().Write<Velocity>().Read<Disabled>(), system)]))
        { Assert.True(scheduler.Tick(Step)); Assert.Equal(1UL, scheduler.TickNumber); }
        Assert.Equal((6, 3), (world.GetComponent<Counter>(first).Value, world.GetComponent<Velocity>(first).Value));
        Assert.Equal((100, 20), (world.GetComponent<Counter>(excluded).Value, world.GetComponent<Velocity>(excluded).Value));
        Assert.Equal(5, callbacks); Assert.Equal((1, 1, 1), (system.Executions, system.Stops, system.Disposals));
        Assert.Equal(3, world.ComponentTypeCount); Assert.False(world.IsFaulted);
    }
    [Fact]
    public void SharedGraph_OrdersLegacyAndNeutralSystems_AndPublishesCommandsOnlyAtPhaseBoundary()
    {
        using var world = new World(); var trace = new List<string>();
        var writer = new SystemProbe(context => { trace.Add("write"); var e = context.Commands.CreateEntity(); context.Commands.AddComponent(e, new Counter(41)); Assert.Equal(0, world.EntityCount); });
        var samePhase = new SystemProbe(context => { trace.Add("same"); var count = 0; context.Query<Counter>().ForEach((A.EntityHandle e, in Counter c) => count++); Assert.Equal(0, count); });
        var reader = new SystemProbe(context => { trace.Add("extract"); var sum = 0; context.Query<Counter>().ForEach((A.EntityHandle e, in Counter c) => sum += c.Value); Assert.Equal(41, sum); });
        var legacy = new LegacyProbe(_ => trace.Add("legacy"));
        using var scheduler = EcsScheduler.Create(world,
        [
            Register(new A.EcsSystemDescription("extract", A.EcsPhase.Extraction).Read<Counter>().After("same"), reader),
            Register(new A.EcsSystemDescription("same").Read<Counter>().After("write"), samePhase),
            new(new EcsSystemDescription("legacy", A.EcsPhase.Input), new Factory<IEcsSystem, World>(_ => legacy)),
            Register(new A.EcsSystemDescription("write").Write<Counter>(), writer)
        ]);
        Assert.True(scheduler.Tick(Step)); Assert.Equal(new[] { "legacy", "write", "same", "extract" }, trace);
        Assert.Equal(1, world.EntityCount); Assert.False(world.IsFaulted);
    }
    [Fact]
    public void NeutralCommandTargets_UpdateRemoveAndDestroyTheSameGenerationalEntity()
    {
        using var world = new World(); var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(1));
        var handle = new A.EntityHandle(world.Id, entity.Index, entity.Generation);
        var system = new SystemProbe(context =>
        {
            if (context.TickNumber == 1) { context.Commands.SetComponent(handle, new Counter(17)); context.Commands.AddComponent(handle, new Velocity(2)); }
            else if (context.TickNumber == 2) context.Commands.RemoveComponent<Velocity>(handle);
            else context.Commands.DestroyEntity(handle);
        });
        using var scheduler = EcsScheduler.Create(world, [Register(new A.EcsSystemDescription("commands").Write<Counter>().Write<Velocity>(), system)]);
        scheduler.Tick(Step); Assert.Equal(17, world.GetComponent<Counter>(entity).Value); Assert.Equal(2, world.GetComponent<Velocity>(entity).Value);
        scheduler.Tick(Step); Assert.False(world.HasComponent<Velocity>(entity)); Assert.Equal(17, world.GetComponent<Counter>(entity).Value);
        scheduler.Tick(Step); Assert.False(world.IsAlive(entity)); Assert.Equal(0, world.EntityCount); Assert.Equal(3UL, scheduler.TickNumber);
    }
    [Fact]
    public void RetainedContextQueryAndWriter_CannotReviveInTheNextInvocation_OrAfterDisposal()
    {
        using var world = new World(); var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(3));
        A.EcsSystemContext old = default; A.EcsQuery<Counter> query = default; A.EcsCommandWriter writer = default;
        var system = new SystemProbe(context =>
        {
            if (context.TickNumber == 1) { old = context; query = context.Query<Counter>(); writer = context.Commands; }
            else
            {
                Assert.Throws<InvalidOperationException>(() => _ = old.WorldId);
                Assert.Throws<InvalidOperationException>(() => old.Query<Counter>());
                Assert.Throws<InvalidOperationException>(() => query.ForEach((A.EntityHandle e, in Counter c) => { }));
                Assert.Throws<InvalidOperationException>(() => writer.CreateEntity());
                Assert.True(context.HasComponent<Counter>(new(world.Id, entity.Index, entity.Generation)));
            }
        });
        var scheduler = EcsScheduler.Create(world, [Register(new A.EcsSystemDescription("retained").Write<Counter>(), system)]);
        scheduler.Tick(Step); Assert.Throws<InvalidOperationException>(() => _ = old.TickNumber); scheduler.Tick(Step); scheduler.Dispose();
        Assert.Throws<InvalidOperationException>(() => _ = old.WorldId); Assert.Throws<InvalidOperationException>(() => writer.CreateEntity());
        Assert.Equal((2, 1, 1), (system.Executions, system.Stops, system.Disposals));
    }
    [Theory]
    [InlineData("read", typeof(InvalidOperationException))]
    [InlineData("write", typeof(InvalidOperationException))]
    [InlineData("excluded", typeof(InvalidOperationException))]
    [InlineData("wrong-world", typeof(ArgumentException))]
    [InlineData("stale", typeof(ArgumentException))]
    [InlineData("nested", typeof(InvalidOperationException))]
    [InlineData("structural", typeof(InvalidOperationException))]
    [InlineData("default-target", typeof(ArgumentException))]
    public void InvalidAccess_IsTerminal_WithoutPublishingCommandsOrTick(string mode, Type expected)
    {
        using var world = new World(); var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(1));
        var metadata = new A.EcsSystemDescription("invalid"); if (mode != "read") metadata.Read<Counter>();
        var system = new SystemProbe(context =>
        {
            switch (mode)
            {
                case "read": context.HasComponent<Counter>(new(world.Id, entity.Index, entity.Generation)); break;
                case "write": context.Query<Counter>().ForEachWrite((A.EntityHandle e, ref Counter c) => c = new(2)); break;
                case "excluded": context.Query<Counter>().Without<Disabled>().ForEach((A.EntityHandle e, in Counter c) => { }); break;
                case "wrong-world": context.HasComponent<Counter>(new(Guid.NewGuid(), 0, 1)); break;
                case "stale": context.HasComponent<Counter>(new(world.Id, entity.Index, entity.Generation + 1)); break;
                case "nested": context.Query<Counter>().ForEach((A.EntityHandle e, in Counter c) => context.Query<Counter>().ForEach((A.EntityHandle e2, in Counter c2) => { })); break;
                case "structural": world.CreateEntity(); break;
                case "default-target": context.Commands.DestroyEntity(default); break;
            }
        });
        using var scheduler = EcsScheduler.Create(world, [Register(metadata, system)]);
        Assert.Throws(expected, () => scheduler.Tick(Step)); Assert.True(world.IsFaulted); Assert.Equal(EcsSchedulerState.Faulted, scheduler.State);
        Assert.Equal(0UL, scheduler.TickNumber); Assert.Equal(1, world.EntityCount); Assert.Equal(new Counter(1), world.GetComponent<Counter>(entity));
        Assert.Throws<InvalidOperationException>(() => scheduler.Tick(Step)); Assert.Equal(1, system.Stops);
    }
    [Fact]
    public void CallbackFailure_PreservesCompletedDirectWrites_AndDiscardsPhaseCommands()
    {
        using var world = new World(); var first = world.CreateEntity(); var second = world.CreateEntity(); world.AddComponent(first, new Counter(1)); world.AddComponent(second, new Counter(1));
        var marker = new IOException("callback marker"); var later = new SystemProbe();
        var system = new SystemProbe(context => { context.Commands.CreateEntity(); context.Query<Counter>().ForEachWrite((A.EntityHandle e, ref Counter c) => { c = new(c.Value + 1); throw marker; }); });
        using var scheduler = EcsScheduler.Create(world, [Register(new A.EcsSystemDescription("first").Write<Counter>(), system), Register(new A.EcsSystemDescription("later").After("first"), later)]);
        Assert.Same(marker, Assert.Throws<IOException>(() => scheduler.Tick(Step)));
        Assert.Equal(3, world.GetComponent<Counter>(first).Value + world.GetComponent<Counter>(second).Value); Assert.Equal(2, world.EntityCount);
        Assert.True(world.IsFaulted); Assert.Equal(0UL, scheduler.TickNumber); Assert.Equal(0, later.Executions); Assert.Equal(1, later.Stops);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationOrRequestedStop_DiscardsPendingCommands_WithoutPoisoningWorld(bool cancel)
    {
        using var world = new World(); using var token = new CancellationTokenSource();
        var system = new SystemProbe(context => { context.Commands.CreateEntity(); if (cancel) token.Cancel(); else context.RequestStop(); });
        using var scheduler = EcsScheduler.Create(world, [Register(new A.EcsSystemDescription("stop"), system)]);
        if (cancel) Assert.ThrowsAny<OperationCanceledException>(() => scheduler.Tick(Step, token.Token)); else Assert.False(scheduler.Tick(Step));
        Assert.Equal(EcsSchedulerState.Stopped, scheduler.State); Assert.Equal(0UL, scheduler.TickNumber); Assert.Equal(0, world.EntityCount); Assert.False(world.IsFaulted); Assert.Equal(1, system.Stops);
    }
    [Fact]
    public void CrossThreadInvocation_IsRejected_WhileHandledViolationLeavesOwnerUsable()
    {
        using var world = new World(); Exception? failure = null;
        var system = new SystemProbe(context =>
        {
            var thread = new Thread(() => failure = Record.Exception(() => _ = context.WorldId)); thread.Start(); thread.Join();
            Assert.IsType<InvalidOperationException>(failure); Assert.Equal(world.Id, context.WorldId);
        });
        using var scheduler = EcsScheduler.Create(world, [Register(new A.EcsSystemDescription("thread"), system)]);
        Assert.True(scheduler.Tick(Step)); Assert.False(world.IsFaulted);
    }
    [Fact]
    public void DuplicateInnerSystemAcrossNeutralAndLegacyRegistrations_IsStoppedAndDisposedOnce()
    {
        using var world = new World(); var shared = new DualProbe();
        Assert.Throws<InvalidOperationException>(() => EcsScheduler.Create(world,
        [new(new EcsSystemDescription("legacy"), new Factory<IEcsSystem, World>(_ => shared)), Register(new A.EcsSystemDescription("new"), shared)]));
        Assert.Equal((1, 1), (shared.Stops, shared.Disposals)); using var retry = EcsScheduler.Create(world, []); Assert.True(retry.Tick(Step));
    }
    [Fact]
    public void FailedCreationAndCleanup_RetainTheSameOwnerUntilSuccessfulRetry()
    {
        using var world = new World(); var system = new SystemProbe { DisposeFailures = 1 };
        var error = Assert.Throws<EcsSchedulerCreationException>(() => EcsScheduler.Create(world,
        [Register(new A.EcsSystemDescription("first"), system), new(new A.EcsSystemDescription("failure").After("first"), new Factory<A.ISystem>(() => throw new IOException("activation marker")))]));
        Assert.Equal((1, 1), (system.Stops, system.Disposals)); Assert.Throws<InvalidOperationException>(() => world.Dispose());
        error.CleanupOwner.Dispose(); Assert.Equal((1, 2), (system.Stops, system.Disposals)); Assert.Equal(EcsSchedulerState.Disposed, error.CleanupOwner.State);
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AppFactories_CreateNeutralSystemsLazily_AndNoResolutionOccursDuringTicks(bool microsoft, bool worldArguments)
    {
        var calls = 0; SystemProbe? system = null; A.EcsSystemDescription? mutable = null;
        await using var app = App.Builder(builder => { if (microsoft) builder.UseDI(); }).Setup(context =>
        {
            context.Services(services =>
            {
                if (worldArguments) services.Singleton<IFactory<SystemProbe, World>>(_ => new Factory<SystemProbe, World>(world => { calls++; return system = new SystemProbe(invocation => Assert.Equal(world.Id, invocation.WorldId)); }));
                else services.Singleton<IFactory<SystemProbe>>(_ => new Factory<SystemProbe>(() => { calls++; return system = new SystemProbe(); }));
            });
            context.UseAetherEcs(); context.UseAetherEcsSystems(builder => { mutable = new A.EcsSystemDescription("app"); builder.Add<SystemProbe>(mutable); });
        }).Build();
        mutable!.After("not-present-after-snapshot"); await app.StartAsync();
        var worlds = app.Services.Resolve<IFactory<World>>()!; var schedulers = app.Services.Resolve<IFactory<EcsScheduler, World>>()!; Assert.Equal(0, calls);
        using var world = worlds.Create()!; using (var scheduler = schedulers.Create(world)) { Assert.Equal(1, calls); for (var i = 0; i < 10; i++) Assert.True(scheduler.Tick(Step)); Assert.Equal(1, calls); }
        await app.StopAsync(); await app.DisposeAsync(); Assert.Equal((10, 1, 1), (system!.Executions, system.Stops, system.Disposals)); Assert.False(world.IsDisposed);
    }
    [Fact]
    public void RetainedNeutralContext_DoesNotKeepDisposedWorldAlive()
    {
        var (world, context) = Retired();
        for (var i = 0; i < 10 && world.IsAlive; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(world.IsAlive); Assert.Throws<InvalidOperationException>(() => _ = context.WorldId);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference, A.EcsSystemContext) Retired()
    {
        var world = new World(); A.EcsSystemContext retained = default; var system = new SystemProbe(context => retained = context);
        var scheduler = EcsScheduler.Create(world, [Register(new A.EcsSystemDescription("retired"), system)]); scheduler.Tick(Step); scheduler.Dispose(); world.Dispose();
        return (new WeakReference(world), retained);
    }
    private sealed class LegacyProbe(Action<EcsSystemContext> execute) : IEcsSystem
    { public void Execute(EcsSystemContext context) => execute(context); public void Dispose() { } }
    private sealed class DualProbe : IEcsSystem, A.IEcsSystem
    {
        public int Stops, Disposals; public void Execute(EcsSystemContext context) { }
        public void Execute(A.EcsSystemContext context) { }
        public void Stop() => Stops++; public void Dispose() => Disposals++;
    }
}
