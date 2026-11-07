using TrueMoon.Aether.Ecs;
using Xunit;
using Xunit.Abstractions;

namespace TrueMoon.Aether.Tests.Ecs;

public sealed class EcsSchedulerTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(20);

    [Theory]
    [InlineData("missing-after")]
    [InlineData("missing-before")]
    [InlineData("cycle")]
    [InlineData("phase-after")]
    [InlineData("phase-before")]
    [InlineData("duplicate-id")]
    [InlineData("count-limit")]
    [InlineData("null-entry")]
    public void InvalidGraph_IsRejectedBeforeAnySystemActivation(string kind)
    {
        using var world = new World();
        var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(11));
        var first = new EcsSystemDescription("first", kind == "phase-before" ? EcsPhase.Movement : EcsPhase.Input);
        var second = new EcsSystemDescription(kind == "duplicate-id" ? "first" : "second",
            kind == "phase-before" ? EcsPhase.Input : kind == "phase-after" ? EcsPhase.Movement : EcsPhase.Input);
        switch (kind)
        {
            case "missing-after": first.After("unknown"); break;
            case "missing-before": first.Before("unknown"); break;
            case "cycle": first.After("second"); second.After("first"); break;
            case "phase-after": first.After("second"); break;
            case "phase-before": first.Before("second"); break;
        }
        var calls = 0;
        var factory = new ProbeFactory(_ => { calls++; return new ProbeSystem(); });
        var entries = new[] { new EcsSystemRegistration(first, factory), new(second, factory) };
        if (kind == "null-entry") entries[1] = null!;
        var options = new EcsSchedulerOptions { MaximumSystems = kind == "count-limit" ? 1 : 2 };
        Assert.Throws<ArgumentException>(() => EcsScheduler.Create(world, entries, options));
        Assert.Equal(0, calls);
        Assert.Equal(new Counter(11), world.GetComponent<Counter>(entity));
        Assert.Equal((1, 1), (world.EntityCount, world.ComponentTypeCount));
        Assert.False(world.IsFaulted);
        using var usable = EcsScheduler.Create(world, []);
        Assert.True(usable.Tick(Step));
    }

    [Fact]
    public void Graph_UsesPhaseThenStableRegistrationOrderWithBeforeAndAfterEdges()
    {
        using var world = new World();
        var trace = new List<string>();
        EcsSystemRegistration Entry(string id, EcsPhase phase, string? after = null, string? before = null)
        {
            var description = new EcsSystemDescription(id, phase);
            if (after != null) description.After(after);
            if (before != null) description.Before(before);
            return Register(description, new ProbeSystem(_ => trace.Add(id)));
        }
        using var scheduler = EcsScheduler.Create(world,
        [
            Entry("extract", EcsPhase.Extraction, after: "move"),
            Entry("game-last", EcsPhase.Gameplay, after: "game-middle"),
            Entry("move", EcsPhase.Movement),
            Entry("game-first", EcsPhase.Gameplay, before: "game-middle"),
            Entry("game-middle", EcsPhase.Gameplay),
            Entry("input-z", EcsPhase.Input),
            Entry("input-A", EcsPhase.Input)
        ]);
        Assert.True(scheduler.Tick(Step));
        Assert.Equal(new[] { "input-z", "input-A", "game-first", "game-middle", "game-last", "move", "extract" }, trace);
        Assert.Equal(1UL, scheduler.TickNumber);
        Assert.Equal(EcsSchedulerState.Ready, scheduler.State);
        Assert.Null(scheduler.Failure);
    }

    [Fact]
    public void PhaseBoundary_CommitsCommandsBeforeNextPhaseAndQueriesObserveMovement()
    {
        using var world = new World();
        var original = world.CreateEntity();
        world.AddComponent(original, new Counter(2)); world.AddComponent(original, new Velocity(3));
        var counts = new List<int>();
        var first = new ProbeSystem(context =>
        {
            var created = context.Commands.CreateEntity();
            context.Commands.AddComponent(created, new Counter(5));
            context.Commands.AddComponent(created, new Velocity(7));
            Assert.Equal(3, context.Commands.Count);
            Assert.Equal(1, context.World.EntityCount);
        });
        var samePhase = new ProbeSystem(context => counts.Add(context.World.EntityCount));
        var move = new ProbeSystem(context =>
        {
            counts.Add(context.World.EntityCount);
            context.World.Query<Counter, Velocity>().ForEachWrite(
                (EntityHandle entity, scoped ref Counter counter, scoped in Velocity velocity) => counter.Value += velocity.Value);
            context.Commands.DestroyEntity(original);
        });
        var extract = new ProbeSystem(context =>
        {
            counts.Add(context.World.EntityCount);
            context.World.Query<Counter>().ForEach((EntityHandle entity, scoped in Counter counter) => Assert.Equal(12, counter.Value));
            Assert.False(context.World.IsAlive(original));
        });
        using var scheduler = EcsScheduler.Create(world,
        [
            Register(new EcsSystemDescription("spawn", EcsPhase.Input).Write<Counter>().Write<Velocity>(), first),
            Register(new("same-phase", EcsPhase.Input), samePhase),
            Register(new EcsSystemDescription("move", EcsPhase.Movement).Write<Counter>().Read<Velocity>(), move),
            Register(new EcsSystemDescription("extract", EcsPhase.Extraction).Read<Counter>(), extract)
        ]);
        Assert.True(scheduler.Tick(Step));
        Assert.Equal(new[] { 1, 2, 1 }, counts);
        Assert.Equal((1, 2), (world.EntityCount, world.ComponentTypeCount));
        Assert.Equal(1UL, scheduler.TickNumber);
        Assert.False(world.IsFaulted);
    }

    [Theory]
    [InlineData("undeclared-read")]
    [InlineData("undeclared-has")]
    [InlineData("undeclared-query")]
    [InlineData("readonly-write")]
    [InlineData("readonly-query-write")]
    [InlineData("second-query-write")]
    [InlineData("undeclared-exclusion")]
    [InlineData("undeclared-command")]
    [InlineData("structural-create")]
    [InlineData("structural-destroy")]
    [InlineData("structural-add")]
    [InlineData("structural-remove")]
    [InlineData("dispose-world")]
    public void AccessViolations_FaultGraphBeforeChangingDataAndSkipLaterSystems(string operation)
    {
        using var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Counter(13)); world.AddComponent(entity, new Velocity(17));
        var description = new EcsSystemDescription("denied").Read<Counter>();
        if (operation is not ("readonly-write" or "readonly-query-write")) description.Write<Counter>();
        if (operation == "second-query-write") description.Read<Velocity>();
        var denied = new ProbeSystem(context =>
        {
            switch (operation)
            {
                case "undeclared-read": context.World.GetComponent<Velocity>(entity); break;
                case "undeclared-has": context.World.HasComponent<Velocity>(entity); break;
                case "undeclared-query": context.World.Query<Velocity>().ForEach((EntityHandle e, scoped in Velocity value) => { }); break;
                case "readonly-write": context.World.SetComponent(entity, new Counter(-1)); break;
                case "readonly-query-write": context.World.Query<Counter>().ForEachWrite((EntityHandle e, scoped ref Counter value) => value.Value = -1); break;
                case "second-query-write": context.World.Query<Counter, Velocity>().ForEachWriteBoth((EntityHandle e, scoped ref Counter c, scoped ref Velocity v) => c.Value = -1); break;
                case "undeclared-exclusion": context.World.Query<Counter>().Without<Blocked>().ForEach((EntityHandle e, scoped in Counter value) => { }); break;
                case "undeclared-command": context.Commands.SetComponent(entity, new Velocity(-1)); break;
                case "structural-create": context.World.CreateEntity(); break;
                case "structural-destroy": context.World.DestroyEntity(entity); break;
                case "structural-add": context.World.AddComponent(entity, new Blocked()); break;
                case "structural-remove": context.World.RemoveComponent<Counter>(entity); break;
                case "dispose-world": context.World.Dispose(); break;
            }
        });
        var later = new ProbeSystem();
        using var scheduler = EcsScheduler.Create(world, [Register(description, denied), Register(new("later"), later)]);
        var error = Assert.Throws<InvalidOperationException>(() => scheduler.Tick(Step));
        Assert.Same(error, scheduler.Failure);
        Assert.Equal(EcsSchedulerState.Faulted, scheduler.State);
        Assert.Equal(0UL, scheduler.TickNumber);
        Assert.True(world.IsFaulted);
        Assert.Equal(new Counter(13), world.GetComponent<Counter>(entity));
        Assert.Equal(new Velocity(17), world.GetComponent<Velocity>(entity));
        Assert.Equal((1, 2), (world.EntityCount, world.ComponentTypeCount));
        Assert.Equal((1, 0, 1, 1), (denied.Executions, later.Executions, denied.Stops, later.Stops));
        Assert.Throws<InvalidOperationException>(() => world.SetComponent(entity, new Counter(19)));
        Assert.Throws<InvalidOperationException>(() => scheduler.Tick(Step));
    }

    [Fact]
    public void DescriptionsAndOptions_AreFrozenBeforeActivationAndWriteIncludesReadAccess()
    {
        using var world = new World();
        var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(23));
        var readOnly = new EcsSystemDescription("read-only").Read<Counter>();
        var first = Register(readOnly, new ProbeSystem(context =>
        {
            Assert.Equal(new Counter(23), context.World.GetComponent<Counter>(entity));
            Assert.Throws<InvalidOperationException>(() => context.World.SetComponent(entity, new Counter(-1)));
        }));
        readOnly.Write<Counter>().After("not-registered");
        var second = Register(new EcsSystemDescription("write").Write<Counter>(), new ProbeSystem(context =>
        {
            Assert.Equal(new Counter(23), context.World.GetComponent<Counter>(entity));
            context.World.SetComponent(entity, new Counter(29));
            context.Commands.SetComponent(entity, new Counter(31));
            context.Commands.SetComponent(entity, new Counter(37));
        }));
        var options = new EcsSchedulerOptions { MaximumSystems = 2, MaximumCommandsPerPhase = 2 };
        using var scheduler = EcsScheduler.Create(world, [first, second], options);
        options.MaximumSystems = 0; options.MaximumCommandsPerPhase = 1;
        Assert.True(scheduler.Tick(Step));
        Assert.Equal(new Counter(37), world.GetComponent<Counter>(entity));
        Assert.Equal(EcsSchedulerState.Ready, scheduler.State);
    }

    [Fact]
    public void ContextsAndWriters_AreScopedToTheirOwnInvocation()
    {
        using var world = new World();
        EcsSystemContext? saved = null; EcsCommandWriter writer = default;
        var first = new ProbeSystem(context =>
        {
            saved = context; writer = context.Commands;
            Assert.Same(world, context.World);
            Assert.Equal((1UL, Step, EcsPhase.Gameplay), (context.TickNumber, context.DeltaTime, context.Phase));
            Assert.Equal(CancellationToken.None, context.CancellationToken);
            context.Commands.CreateEntity();
            Assert.Equal(1, context.Commands.Count);
        });
        var second = new ProbeSystem(context =>
        {
            Assert.NotSame(saved, context);
            Assert.Throws<InvalidOperationException>(() => saved!.World);
            Assert.Throws<InvalidOperationException>(() => writer.CreateEntity());
            Assert.Equal(1, context.Commands.Count);
        });
        using var scheduler = EcsScheduler.Create(world, [Register(new("first"), first), Register(new("second"), second)]);
        Assert.True(scheduler.Tick(Step));
        Assert.NotNull(saved);
        Assert.Throws<InvalidOperationException>(() => saved.World);
        Assert.Throws<InvalidOperationException>(() => saved.Commands);
        Assert.Throws<InvalidOperationException>(() => saved.TickNumber);
        Assert.Throws<InvalidOperationException>(() => saved.DeltaTime);
        Assert.Throws<InvalidOperationException>(() => saved.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => saved.Phase);
        Assert.Throws<InvalidOperationException>(() => saved.RequestStop());
        Assert.Throws<InvalidOperationException>(() => writer.Count);
        Assert.Throws<InvalidOperationException>(() => writer.CreateEntity());
        Assert.Throws<InvalidOperationException>(() => writer.DestroyEntity(default));
        Assert.Throws<InvalidOperationException>(() => writer.AddComponent(default, new Counter(1)));
        Assert.Throws<InvalidOperationException>(() => writer.SetComponent(default, new Counter(1)));
        Assert.Throws<InvalidOperationException>(() => writer.RemoveComponent<Counter>(default));
        Assert.Throws<InvalidOperationException>(() => default(EcsCommandWriter).Count);
        Assert.Throws<InvalidOperationException>(() => default(EcsCommandWriter).CreateEntity());
        Assert.Equal(1, world.EntityCount);
    }

    [Fact]
    public void RetainedWriter_IsRejectedDuringNextInvocationWhileFreshWriterRecordsCorrectly()
    {
        using var world = new World();
        var original = world.CreateEntity(); world.AddComponent(original, new Counter(79));
        EcsCommandWriter retained = default;
        var system = new ProbeSystem(context =>
        {
            var fresh = context.Commands;
            Assert.Equal(0, fresh.Count);
            if (context.TickNumber == 1)
            {
                var created = fresh.CreateEntity(); fresh.AddComponent(created, new Counter(83));
                retained = fresh;
                Assert.Equal(2, retained.Count);
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => retained.Count);
                Assert.Throws<InvalidOperationException>(() => retained.CreateEntity());
                Assert.Throws<InvalidOperationException>(() => retained.DestroyEntity(original));
                Assert.Throws<InvalidOperationException>(() => retained.AddComponent(original, new Velocity(-1)));
                Assert.Throws<InvalidOperationException>(() => retained.SetComponent(original, new Counter(-1)));
                Assert.Throws<InvalidOperationException>(() => retained.RemoveComponent<Counter>(original));
                Assert.Equal(0, fresh.Count);
                var created = fresh.CreateEntity(); fresh.AddComponent(created, new Counter(89));
                fresh.SetComponent(original, new Counter(97));
                Assert.Equal(3, fresh.Count);
            }
        });
        using var scheduler = EcsScheduler.Create(world,
            [Register(new EcsSystemDescription("scoped-writer").Write<Counter>().Write<Velocity>(), system)]);
        Assert.True(scheduler.Tick(Step));
        Assert.Equal(new Counter(79), world.GetComponent<Counter>(original));
        Assert.Equal(2, world.EntityCount);
        Assert.True(scheduler.Tick(Step));
        var values = new List<int>();
        world.Query<Counter>().ForEach((EntityHandle e, scoped in Counter value) => values.Add(value.Value));
        Assert.Equal(new[] { 83, 89, 97 }, values.OrderBy(value => value));
        Assert.False(world.HasComponent<Velocity>(original));
        Assert.Equal((3, 1), (world.EntityCount, world.ComponentTypeCount));
        Assert.Equal((2UL, EcsSchedulerState.Ready), (scheduler.TickNumber, scheduler.State));
    }

    [Fact]
    public void Tick_RejectsReentrancyAndDisposalWithoutDamagingReadyGraph()
    {
        using var world = new World();
        EcsScheduler? scheduler = null;
        var system = new ProbeSystem(context =>
        {
            Assert.Equal(EcsSchedulerState.Running, scheduler!.State);
            Assert.Throws<InvalidOperationException>(() => scheduler.Tick(Step));
            Assert.Throws<InvalidOperationException>(() => scheduler.Dispose());
            Assert.Throws<InvalidOperationException>(() => context.World.Dispose());
        });
        using (scheduler = EcsScheduler.Create(world, [Register(new("one"), system)]))
        {
            Assert.True(scheduler.Tick(Step)); Assert.True(scheduler.Tick(Step));
            Assert.Equal((2UL, 2), (scheduler.TickNumber, system.Executions));
            Assert.Equal(EcsSchedulerState.Ready, scheduler.State);
            Assert.False(world.IsFaulted);
        }
        Assert.Equal((1, 1), (system.Stops, system.Disposals));
    }

    [Fact]
    public void PreCancelledTick_LeavesReadyGraphUnchangedAndAllowsExplicitRetry()
    {
        using var world = new World();
        var system = new ProbeSystem();
        using var scheduler = EcsScheduler.Create(world, [Register(new("one"), system)]);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => scheduler.Tick(Step, cancellation.Token));
        Assert.Equal((0UL, EcsSchedulerState.Ready), (scheduler.TickNumber, scheduler.State));
        Assert.Equal((0, 0), (system.Executions, system.Stops));
        Assert.Null(scheduler.Failure);
        Assert.False(world.IsFaulted);
        Assert.True(scheduler.Tick(Step));
        Assert.Equal((1UL, 1), (scheduler.TickNumber, system.Executions));
    }

    [Fact]
    public void CancellationAfterStart_StopsGraphAndDiscardsPendingPhaseCommands()
    {
        using var world = new World();
        var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(41));
        using var cancellation = new CancellationTokenSource();
        var first = new ProbeSystem(context =>
        {
            context.World.SetComponent(entity, new Counter(43));
            context.Commands.CreateEntity();
            cancellation.Cancel();
        });
        var later = new ProbeSystem();
        using var scheduler = EcsScheduler.Create(world,
            [Register(new EcsSystemDescription("cancel").Write<Counter>(), first), Register(new("later"), later)]);
        var error = Assert.Throws<OperationCanceledException>(() => scheduler.Tick(Step, cancellation.Token));
        Assert.Same(error, scheduler.Failure);
        Assert.Equal((0UL, EcsSchedulerState.Stopped), (scheduler.TickNumber, scheduler.State));
        Assert.Equal((1, 0, 1, 1), (first.Executions, later.Executions, first.Stops, later.Stops));
        Assert.Equal(new Counter(43), world.GetComponent<Counter>(entity));
        Assert.Equal(1, world.EntityCount);
        Assert.False(world.IsFaulted);
        Assert.Throws<InvalidOperationException>(() => scheduler.Tick(Step));
    }

    [Theory]
    [InlineData("context")]
    [InlineData("stop")]
    [InlineData("request")]
    public void CooperativeStop_WaitsForCallbackDiscardsCommandsAndStopsInReverseOrder(string mode)
    {
        using var world = new World();
        var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(47));
        var trace = new List<string>(); EcsScheduler? scheduler = null;
        var first = new ProbeSystem(context =>
        {
            trace.Add("execute-start");
            context.World.SetComponent(entity, new Counter(53));
            context.Commands.CreateEntity();
            if (mode == "context") context.RequestStop();
            else if (mode == "stop") scheduler!.Stop();
            else scheduler!.RequestStop();
            trace.Add("execute-end");
        }, () => trace.Add("stop-first"), () => trace.Add("dispose-first"));
        var second = new ProbeSystem(stop: () => trace.Add("stop-second"), dispose: () => trace.Add("dispose-second"));
        using (scheduler = EcsScheduler.Create(world,
            [Register(new EcsSystemDescription("first").Write<Counter>(), first), Register(new("second"), second)]))
        {
            Assert.False(scheduler.Tick(Step));
            Assert.Equal(new[] { "execute-start", "execute-end", "stop-second", "stop-first" }, trace);
            Assert.Equal((0UL, EcsSchedulerState.Stopped), (scheduler.TickNumber, scheduler.State));
            Assert.Equal(new Counter(53), world.GetComponent<Counter>(entity));
            Assert.Equal(1, world.EntityCount);
            Assert.Equal(0, second.Executions);
            Assert.False(world.IsFaulted);
            Assert.Throws<InvalidOperationException>(() => world.Dispose());
            scheduler.Stop();
            Assert.Equal(4, trace.Count);
        }
        Assert.Equal(new[] { "execute-start", "execute-end", "stop-second", "stop-first", "dispose-second", "dispose-first" }, trace);
        Assert.False(world.IsDisposed);
    }

    [Fact]
    public void CancellationWithStopFailure_PreservesCancellationAndFaultsWorld()
    {
        using var world = new World();
        var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(101));
        using var cancellation = new CancellationTokenSource();
        var cleanup = new IOException("cancellation stop fixture");
        var trace = new List<string>();
        var first = new ProbeSystem(context =>
        {
            context.World.SetComponent(entity, new Counter(103));
            context.Commands.CreateEntity();
            cancellation.Cancel();
        }, () => { trace.Add("stop-first"); throw cleanup; });
        var later = new ProbeSystem(stop: () => trace.Add("stop-later"));
        using var scheduler = EcsScheduler.Create(world,
            [Register(new EcsSystemDescription("cancel").Write<Counter>(), first), Register(new("later"), later)]);

        var combined = Assert.Throws<AggregateException>(() => scheduler.Tick(Step, cancellation.Token));
        var original = Assert.IsType<OperationCanceledException>(combined.InnerExceptions[0]);
        Assert.Equal(cancellation.Token, original.CancellationToken);
        Assert.Contains(cleanup, combined.InnerExceptions);
        Assert.Same(original, scheduler.Failure);
        Assert.Equal((EcsSchedulerState.Faulted, 0UL), (scheduler.State, scheduler.TickNumber));
        Assert.Equal(new[] { "stop-later", "stop-first" }, trace);
        Assert.Equal((1, 0, 1, 1), (first.Executions, later.Executions, first.Stops, later.Stops));
        Assert.Equal(new Counter(103), world.GetComponent<Counter>(entity));
        Assert.Equal(1, world.EntityCount);
        Assert.True(world.IsFaulted);
        Assert.Throws<InvalidOperationException>(() => world.SetComponent(entity, new Counter(-1)));
        Assert.Throws<InvalidOperationException>(() => scheduler.Tick(Step));
    }

    [Fact]
    public void ExplicitStopFailure_AttemptsEveryCallbackOnceAndRetainsFaultUntilDisposal()
    {
        using var world = new World();
        var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(107));
        var failure = new IOException("explicit stop fixture"); var trace = new List<string>();
        var first = new ProbeSystem(stop: () => trace.Add("stop-first"), dispose: () => trace.Add("dispose-first"));
        var second = new ProbeSystem(stop: () => { trace.Add("stop-second"); throw failure; },
            dispose: () => trace.Add("dispose-second"));
        var scheduler = EcsScheduler.Create(world, [Register(new("first"), first), Register(new("second"), second)]);
        try
        {
            var error = Assert.Throws<AggregateException>(() => scheduler.Stop());
            Assert.Contains(failure, error.InnerExceptions);
            Assert.Same(error, scheduler.Failure);
            Assert.Equal((EcsSchedulerState.Faulted, 0UL), (scheduler.State, scheduler.TickNumber));
            Assert.Equal((0, 0, 1, 1), (first.Executions, second.Executions, first.Stops, second.Stops));
            Assert.Equal(new[] { "stop-second", "stop-first" }, trace);
            Assert.Equal(new Counter(107), world.GetComponent<Counter>(entity));
            Assert.True(world.IsFaulted);
            Assert.Same(error, Assert.Throws<InvalidOperationException>(() => world.SetComponent(entity, new Counter(-1))).InnerException);
            Assert.Throws<InvalidOperationException>(() => world.Dispose());
            scheduler.Stop();
            Assert.Equal(2, trace.Count);
            scheduler.Dispose();
            Assert.Equal(new[] { "stop-second", "stop-first", "dispose-second", "dispose-first" }, trace);
            Assert.Equal((1, 1), (first.Disposals, second.Disposals));
            Assert.Equal(EcsSchedulerState.Disposed, scheduler.State);
            Assert.False(world.IsDisposed);
        }
        finally { scheduler.Dispose(); }
    }

    [Fact]
    public void SystemFailure_PreservesEarlierPhaseAndDirectWritesButDiscardsPendingCommands()
    {
        using var world = new World();
        var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(59));
        var expected = new IOException("execution fixture"); var stopFailure = new IOException("stop fixture");
        var prepare = new ProbeSystem(context =>
        {
            var created = context.Commands.CreateEntity(); context.Commands.AddComponent(created, new Counter(61));
        });
        var failing = new ProbeSystem(context =>
        {
            context.World.SetComponent(entity, new Counter(67));
            context.Commands.CreateEntity();
            throw expected;
        }, () => throw stopFailure);
        var later = new ProbeSystem();
        using var scheduler = EcsScheduler.Create(world,
        [
            Register(new EcsSystemDescription("prepare", EcsPhase.Input).Write<Counter>(), prepare),
            Register(new EcsSystemDescription("failure", EcsPhase.Gameplay).Write<Counter>(), failing),
            Register(new("later", EcsPhase.Extraction), later)
        ]);
        var error = Assert.Throws<AggregateException>(() => scheduler.Tick(Step));
        Assert.Contains(expected, error.InnerExceptions); Assert.Contains(stopFailure, error.InnerExceptions);
        Assert.Same(expected, scheduler.Failure);
        Assert.Equal(EcsSchedulerState.Faulted, scheduler.State);
        Assert.Equal(0UL, scheduler.TickNumber);
        Assert.Equal((1, 1, 0), (prepare.Executions, failing.Executions, later.Executions));
        Assert.Equal((1, 1, 1), (prepare.Stops, failing.Stops, later.Stops));
        Assert.Equal(2, world.EntityCount);
        Assert.Equal(new Counter(67), world.GetComponent<Counter>(entity));
        Assert.True(world.IsFaulted);
        Assert.Throws<InvalidOperationException>(() => world.CreateEntity());
    }

    [Fact]
    public void SchedulerLease_IsExclusiveAndDoesNotOwnWorldOrBorrowedFactories()
    {
        using var world = new World();
        var system = new ProbeSystem(); var factory = new ProbeFactory(_ => system);
        var scheduler = EcsScheduler.Create(world, [new(new("one"), factory)]);
        var unused = new ProbeFactory(_ => new ProbeSystem());
        try
        {
            Assert.Throws<InvalidOperationException>(() => EcsScheduler.Create(world, [new(new("two"), unused)]));
            Assert.Equal(0, unused.Calls);
            Assert.Throws<InvalidOperationException>(() => world.Dispose());
            Assert.True(scheduler.Tick(Step));
        }
        finally { scheduler.Dispose(); }
        scheduler.Dispose();
        Assert.Equal((1, 1, 0), (system.Stops, system.Disposals, factory.Disposals));
        Assert.Equal(EcsSchedulerState.Disposed, scheduler.State);
        Assert.Throws<ObjectDisposedException>(() => scheduler.RequestStop());
        Assert.Throws<InvalidOperationException>(() => scheduler.Tick(Step));
        Assert.False(world.IsDisposed);
        Assert.True(world.IsAlive(world.CreateEntity()));
        using var replacement = EcsScheduler.Create(world, []);
        Assert.True(replacement.Tick(Step));
    }

    [Theory]
    [InlineData("throws")]
    [InlineData("null")]
    [InlineData("alias")]
    public void FailedActivation_CleansAcquiredSystemsOnceInReverseOrderAndReleasesLease(string kind)
    {
        using var world = new World();
        var trace = new List<string>(); var expected = new IOException("factory fixture");
        var first = new ProbeSystem(stop: () => trace.Add("stop-a"), dispose: () => trace.Add("dispose-a"));
        var second = new ProbeSystem(stop: () => trace.Add("stop-b"), dispose: () => trace.Add("dispose-b"));
        var final = new ProbeFactory(_ => kind switch
        {
            "throws" => throw expected,
            "null" => null!,
            "alias" => first,
            _ => throw new ArgumentException(nameof(kind))
        });
        var entries = new[] { Register(new("a"), first), Register(new("b"), second), new EcsSystemRegistration(new("c"), final) };
        if (kind == "throws") Assert.Same(expected, Assert.Throws<IOException>(() => EcsScheduler.Create(world, entries)));
        else Assert.Throws<InvalidOperationException>(() => EcsScheduler.Create(world, entries));
        Assert.Equal(new[] { "stop-b", "stop-a", "dispose-b", "dispose-a" }, trace);
        Assert.Equal((1, 1, 1, 1), (first.Stops, second.Stops, first.Disposals, second.Disposals));
        Assert.Equal(0, final.Disposals);
        Assert.False(world.IsFaulted);
        using var recovered = EcsScheduler.Create(world, []);
        Assert.True(recovered.Tick(Step));
    }

    [Fact]
    public void FactoryActivation_AllowsInspectionAndRejectsWorldWritesAndPlayback()
    {
        using var world = new World();
        var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(71));
        using var commands = new EntityCommandBuffer(world);
        var factory = new ProbeFactory(owner =>
        {
            Assert.Same(world, owner);
            Assert.Equal(new Counter(71), owner.GetComponent<Counter>(entity));
            Assert.Throws<InvalidOperationException>(() => owner.CreateEntity());
            Assert.Throws<InvalidOperationException>(() => owner.DestroyEntity(entity));
            Assert.Throws<InvalidOperationException>(() => owner.AddComponent(entity, new Velocity(1)));
            Assert.Throws<InvalidOperationException>(() => owner.RemoveComponent<Counter>(entity));
            Assert.Throws<InvalidOperationException>(() => owner.SetComponent(entity, new Counter(-1)));
            Assert.Throws<InvalidOperationException>(() => owner.Query<Counter>().ForEachWrite((EntityHandle e, scoped ref Counter value) => value.Value = -1));
            Assert.Throws<InvalidOperationException>(() => commands.Playback());
            owner.Query<Counter>().ForEach((EntityHandle e, scoped in Counter value) => Assert.Equal(71, value.Value));
            return new ProbeSystem();
        });
        using var scheduler = EcsScheduler.Create(world, [new(new("one"), factory)]);
        Assert.Equal(1, factory.Calls);
        Assert.Equal((1, 1), (world.EntityCount, world.ComponentTypeCount));
        Assert.True(scheduler.Tick(Step));
    }

    [Fact]
    public void FailedActivationCleanup_RetainsOwnerAndWorldLeaseUntilExplicitRetry()
    {
        using var world = new World();
        var creation = new IOException("creation fixture"); var cleanup = new IOException("cleanup fixture");
        var attempts = 0;
        var first = new ProbeSystem(dispose: () => { if (++attempts == 1) throw cleanup; });
        var error = Assert.Throws<EcsSchedulerCreationException>(() => EcsScheduler.Create(world,
        [Register(new("first"), first), new(new("failure"), new ProbeFactory(_ => throw creation))]));
        var owner = error.CleanupOwner;
        try
        {
            var combined = Assert.IsType<AggregateException>(error.InnerException);
            Assert.Same(creation, combined.InnerExceptions[0]);
            Assert.Equal(EcsSchedulerState.Faulted, owner.State);
            Assert.Equal((1, 1), (first.Stops, first.Disposals));
            Assert.Throws<InvalidOperationException>(() => world.Dispose());
            owner.Dispose();
            Assert.Equal((1, 2), (first.Stops, first.Disposals));
            Assert.Equal(EcsSchedulerState.Disposed, owner.State);
            Assert.True(world.IsAlive(world.CreateEntity()));
        }
        finally { owner.Dispose(); }
    }

    [Fact]
    public void Dispose_IsBestEffortAndRetriesOnlyFailedResourcesBeforeReleasingLease()
    {
        using var world = new World();
        var trace = new List<string>(); var failure = new IOException("dispose fixture"); var attempts = 0;
        var first = new ProbeSystem(stop: () => trace.Add("stop-a"), dispose: () => trace.Add("dispose-a"));
        var second = new ProbeSystem(stop: () => trace.Add("stop-b"), dispose: () =>
        { trace.Add("dispose-b"); if (++attempts == 1) throw failure; });
        var third = new ProbeSystem(stop: () => trace.Add("stop-c"), dispose: () => trace.Add("dispose-c"));
        var scheduler = EcsScheduler.Create(world, [Register(new("a"), first), Register(new("b"), second), Register(new("c"), third)]);
        try
        {
            var error = Assert.Throws<AggregateException>(() => scheduler.Dispose());
            Assert.Contains(failure, error.InnerExceptions);
            Assert.Equal(new[] { "stop-c", "stop-b", "stop-a", "dispose-c", "dispose-b", "dispose-a" }, trace);
            Assert.Equal(EcsSchedulerState.Faulted, scheduler.State);
            Assert.Throws<InvalidOperationException>(() => world.Dispose());
            scheduler.Dispose(); scheduler.Dispose();
            Assert.Equal((1, 2, 1), (first.Disposals, second.Disposals, third.Disposals));
            Assert.Equal((1, 1, 1), (first.Stops, second.Stops, third.Stops));
            Assert.Equal(EcsSchedulerState.Disposed, scheduler.State);
            Assert.Equal(7, trace.Count);
            Assert.Equal("dispose-b", trace[^1]);
            Assert.False(world.IsDisposed);
        }
        finally { scheduler.Dispose(); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidDeltaTime_DoesNotStartTickOrFaultGraph(long ticks)
    {
        using var world = new World(); var system = new ProbeSystem();
        using var scheduler = EcsScheduler.Create(world, [Register(new("one"), system)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Tick(TimeSpan.FromTicks(ticks)));
        Assert.Equal((EcsSchedulerState.Ready, 0UL, 0), (scheduler.State, scheduler.TickNumber, system.Executions));
        Assert.True(scheduler.Tick(TimeSpan.MaxValue));
        Assert.Equal((1UL, 1), (scheduler.TickNumber, system.Executions));
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(-1, 1)] [InlineData(1025, 1)]
    [InlineData(1, 0)] [InlineData(1, -1)] [InlineData(1, 1048577)]
    public void InvalidSchedulerOptions_AreRejectedBeforeActivation(int systems, int commands)
    {
        using var world = new World(); var factory = new ProbeFactory(_ => new ProbeSystem());
        Assert.Throws<ArgumentOutOfRangeException>(() => EcsScheduler.Create(world, [new(new("one"), factory)],
            new() { MaximumSystems = systems, MaximumCommandsPerPhase = commands }));
        Assert.Equal(0, factory.Calls);
        using var valid = EcsScheduler.Create(world, []);
        Assert.True(valid.Tick(Step));
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(1024, 1048576)]
    public void EmptyGraph_AcceptsOptionBoundariesAndCompletesOneCallerDrivenTick(int systems, int commands)
    {
        using var world = new World();
        var scheduler = EcsScheduler.Create(world, [], new() { MaximumSystems = systems, MaximumCommandsPerPhase = commands });
        Assert.True(scheduler.Tick(Step));
        Assert.Equal((EcsSchedulerState.Ready, 1UL), (scheduler.State, scheduler.TickNumber));
        scheduler.RequestStop();
        Assert.False(scheduler.Tick(Step));
        Assert.Equal((EcsSchedulerState.Stopped, 1UL), (scheduler.State, scheduler.TickNumber));
        scheduler.Dispose();
        Assert.True(world.IsAlive(world.CreateEntity()));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("a/b")] [InlineData("русский")] [InlineData("a\nb")]
    public void Description_RejectsInvalidIds(string? id)
        => Assert.ThrowsAny<ArgumentException>(() => new EcsSystemDescription(id!));

    [Fact]
    public void Description_ValidatesIdLengthPhaseTypesDependenciesAndRegistrationArguments()
    {
        var id = new string('a', 128);
        var description = new EcsSystemDescription(id, EcsPhase.Transform);
        Assert.Equal((id, EcsPhase.Transform), (description.Id, description.Phase));
        Assert.Throws<ArgumentException>(() => new EcsSystemDescription(id + "a"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EcsSystemDescription("phase", (EcsPhase)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EcsSystemDescription("phase", (EcsPhase)99));
        Assert.Throws<NotSupportedException>(() => description.Read<ManagedValue>());
        Assert.Throws<NotSupportedException>(() => description.Write<ManagedValue>());
        Assert.Throws<ArgumentException>(() => description.After(id));
        Assert.Throws<ArgumentException>(() => description.Before(id));
        Assert.ThrowsAny<ArgumentException>(() => description.After(""));
        Assert.ThrowsAny<ArgumentException>(() => description.Before(""));
        Assert.Same(description, description.Read<Counter>().Read<Counter>().Write<Counter>().Write<Counter>());
        Assert.Throws<ArgumentNullException>(() => new EcsSystemRegistration(null!, new ProbeFactory(_ => new ProbeSystem())));
        Assert.Throws<ArgumentNullException>(() => new EcsSystemRegistration(description, null!));
        Assert.Throws<ArgumentNullException>(() => EcsScheduler.Create(null!, []));
        using var world = new World();
        Assert.Throws<ArgumentNullException>(() => EcsScheduler.Create(world, null!));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Description_DependencyBudgetCountsDistinctIdsAndRejectsOneBeyondLimit(bool before)
    {
        var description = new EcsSystemDescription("owner");
        for (var i = 0; i < 1024; i++) Add("dependency-" + i);
        Add("dependency-0");
        Assert.Throws<InvalidOperationException>(() => Add("beyond-limit"));
        void Add(string id) { if (before) description.Before(id); else description.After(id); }
    }

    [Fact]
    public void CachedQueryAndDelegates_ReportSteadyAllocationsWithVerifiedState()
    {
        using var world = new World();
        var entity = world.CreateEntity(); world.AddComponent(entity, new Counter(0));
        var query = world.Query<Counter>();
        QueryWriteAction<Counter> increment = (EntityHandle e, scoped ref Counter value) => value.Value++;
        using var scheduler = EcsScheduler.Create(world,
            [Register(new EcsSystemDescription("increment").Write<Counter>(), new ProbeSystem(_ => query.ForEachWrite(increment)))]);
        const int warmup = 256, iterations = 10000;
        for (var i = 0; i < warmup; i++) Assert.True(scheduler.Tick(Step));
        var start = GC.GetAllocatedBytesForCurrentThread();
        var completed = true;
        for (var i = 0; i < iterations; i++) completed &= scheduler.Tick(Step);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.True(completed);
        Assert.Equal(new Counter(warmup + iterations), world.GetComponent<Counter>(entity));
        Assert.Equal((ulong)(warmup + iterations), scheduler.TickNumber);
        Assert.Equal((1, 1), (world.EntityCount, world.ComponentTypeCount));
        output.WriteLine("EcsSteadyAllocations: ticks={0}; allocatedBytes={1}; bytesPerTick={2:F3}",
            iterations, allocated, (double)allocated / iterations);
    }

    private static EcsSystemRegistration Register(EcsSystemDescription description, ProbeSystem system)
        => new(description, new ProbeFactory(_ => system));
    private struct Counter(int value) { internal int Value = value; }
    private struct Velocity(int value) { internal int Value = value; }
    private readonly struct Blocked { }
    private readonly record struct ManagedValue(string Text);
    private sealed class ProbeFactory(Func<World, IEcsSystem> create) : IFactory<IEcsSystem, World>, IDisposable
    {
        internal int Calls, Disposals;
        public IEcsSystem Create(World world) { Calls++; return create(world); }
        public void Dispose() => Disposals++;
    }
    private sealed class ProbeSystem(Action<EcsSystemContext>? execute = null, Action? stop = null, Action? dispose = null) : IEcsSystem
    {
        internal int Executions, Stops, Disposals;
        public void Execute(EcsSystemContext context) { Executions++; execute?.Invoke(context); }
        public void Stop() { Stops++; stop?.Invoke(); }
        public void Dispose() { Disposals++; dispose?.Invoke(); }
    }
}
