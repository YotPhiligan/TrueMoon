namespace TrueMoon.Kton.Bepu;

// Internal acceptance seam only. No user delegates enter backend callbacks or the public provider API.
internal enum Checkpoint { PoolCreated, DispatcherCreated, SimulationCreated, BeforeMutation, AfterCommandCommit, StepBegun, BeforeSimulationDispose, BeforeDispatcherDispose, BeforePoolClear, HostStarting }
internal sealed class FaultInjection
{
    internal Checkpoint? Armed;
    internal Action? Action;
    internal bool FailContactCallback;
    internal void Visit(Checkpoint point)
    {
        if (Armed != point) return;
        Armed = null; var action = Action; Action = null;
        if (action is not null) action(); else throw new InvalidOperationException($"Injected {point}");
    }
}
