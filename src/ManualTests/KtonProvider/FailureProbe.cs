using System.Numerics;
using TrueMoon.Kton;
using TrueMoon.Kton.Bepu;
using TrueMoon.Kton.Bodies;

namespace KtonProviderProbe;

internal static partial class Program
{
    private static void FailureAndCleanup()
    {
        {
            using var factory = new BepuPhysicsWorldFactory();
            using (var world = factory.Create(Options(limits: new() { PoolDiagnosticBudgetBytes = 1 })))
            { Step(world, 2); Check("pool-budget-is-observable-not-hard-cap", world.State == PhysicsWorldState.Ready && factory.Resources.PoolBudgetExceededOwners == 1); }
            Clean("diagnostic-overrun-world-clean", factory);
        }
        foreach (var point in new[] { Checkpoint.PoolCreated, Checkpoint.DispatcherCreated, Checkpoint.SimulationCreated })
        {
            var faults = new FaultInjection { Armed = point }; using var factory = new BepuPhysicsWorldFactory(faults);
            Reject($"failed-creation-{point}", PhysicsErrorCode.Faulted, () => factory.Create(Options())); Clean($"failed-creation-{point}-clean", factory);
        }
        {
            var faults = new FaultInjection { Armed = Checkpoint.SimulationCreated };
            faults.Action = () => { faults.Armed = Checkpoint.BeforePoolClear; throw new InvalidOperationException("Creation failure with cleanup failure"); };
            using var factory = new BepuPhysicsWorldFactory(faults);
            Reject("failed-creation-retains-cleanup-owner", PhysicsErrorCode.Faulted, () => factory.Create(Options()));
            Check("failed-creation-owner-observable", factory.Resources.ActiveResourceOwners == 1 && factory.Resources.PublishedWorlds == 0 && factory.Resources.PendingFailedCreationCleanup, factory.Resources);
            Reject("pending-cleanup-blocks-next-create", PhysicsErrorCode.Faulted, () => factory.Create(Options()));
            factory.Dispose(); Clean("failed-creation-cleanup-retry", factory);
        }
        foreach (var point in new[] { Checkpoint.BeforeSimulationDispose, Checkpoint.BeforeDispatcherDispose, Checkpoint.BeforePoolClear })
        {
            var faults = new FaultInjection(); using var factory = new BepuPhysicsWorldFactory(faults); var world = factory.Create(Options());
            Ball(world, Vector3.Zero); Step(world, 1); faults.Armed = point;
            Reject($"cleanup-failure-{point}", PhysicsErrorCode.Faulted, world.Dispose);
            Check($"cleanup-failure-{point}-retains-owner", world.State == PhysicsWorldState.Disposing && factory.Resources.ActiveResourceOwners == 1 && factory.Resources.PublishedWorlds == 1);
            Reject($"cleanup-failure-{point}-blocks-step", PhysicsErrorCode.Faulted, () => world.Step(2));
            world.Dispose(); world.Dispose(); Clean($"cleanup-retry-{point}-clean", factory);
        }
        {
            var faults = new FaultInjection(); using var factory = new BepuPhysicsWorldFactory(faults); using var world = factory.Create(Options());
            var body = Ball(world, Vector3.Zero); faults.Armed = Checkpoint.AfterCommandCommit;
            Reject("unexpected-command-commit-fault", PhysicsErrorCode.Faulted, () => world.ApplyCommands([new(PhysicsCommandKind.SetVelocity, body, new(2, 0, 0))]));
            var impl = (BepuWorld)world;
            Near("partial-command-not-rolled-back", impl.Resources.Simulation!.Bodies[impl.Bodies[body.Slot]!.Handle].Velocity.Linear.X, 2);
            Check("partial-command-faulted-no-tick", world.State == PhysicsWorldState.Faulted && world.LastCompletedTick == 0);
            Reject("faulted-world-blocks-read", PhysicsErrorCode.Faulted, () => world.GetBodyState(body));
            Reject("faulted-world-blocks-step", PhysicsErrorCode.Faulted, () => world.Step(1));
            world.Dispose(); Clean("partial-command-fault-clean", factory);
        }
        {
            var faults = new FaultInjection { FailContactCallback = true }; using var factory = new BepuPhysicsWorldFactory(faults); using var world = factory.Create(Options(new(0, -9.81f, 0)));
            Floor(world); Ball(world, new(0, 0.2f, 0));
            Reject("callback-error-reported-after-join", PhysicsErrorCode.Faulted, () => world.Step(1));
            Check("callback-error-does-not-publish-tick", world.State == PhysicsWorldState.Faulted && world.LastCompletedTick == 0);
            world.Dispose(); Clean("callback-fault-clean", factory);
        }
        {
            using var factory = new BepuPhysicsWorldFactory(); using var world = factory.Create(Options(new(0, -1000, 0)));
            Ball(world, Vector3.Zero, velocity: new(0, -60, 0));
            Reject("post-integration-envelope-faults", PhysicsErrorCode.Faulted, () => world.Step(1));
            Check("no-clamp-or-false-published-tick", world.State == PhysicsWorldState.Faulted && world.LastCompletedTick == 0);
            world.Dispose(); Clean("integration-envelope-fault-clean", factory);
        }
        {
            var faults = new FaultInjection(); using var factory = new BepuPhysicsWorldFactory(faults); using var world = factory.Create(Options()); var body = Ball(world, Vector3.Zero);
            using var cancel = new CancellationTokenSource(); faults.Armed = Checkpoint.StepBegun; faults.Action = cancel.Cancel;
            world.Step(1, cancel.Token); Check("cancel-after-begin-finishes-step", cancel.IsCancellationRequested && world.State == PhysicsWorldState.Ready && world.LastCompletedTick == 1);
            using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
            faults.Armed = Checkpoint.StepBegun; faults.Action = () => { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); };
            var task = Task.Run(() => world.Step(2));
            try
            {
                if (!entered.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                Reject("concurrent-read-busy", PhysicsErrorCode.Busy, () => world.GetBodyState(body));
                Reject("concurrent-dispose-busy", PhysicsErrorCode.Busy, world.Dispose);
                Check("stepping-state-observable", world.State == PhysicsWorldState.Stepping);
            }
            finally { release.Set(); task.GetAwaiter().GetResult(); }
            Check("busy-call-does-not-abort-owner-step", world.LastCompletedTick == 2 && world.State == PhysicsWorldState.Ready);
            world.Dispose(); Clean("cancel-busy-world-clean", factory);
        }
    }
}
