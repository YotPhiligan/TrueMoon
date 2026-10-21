namespace TrueMoon.Alloy.Runtime;

/// <summary>Independent limits for pending commands and commands executed in one Update.</summary>
public sealed record UiDispatchOptions
{
    /// <summary>Maximum queued commands, excluding a command already running. Overflow is rejected.</summary>
    public int Capacity { get; init; } = 1024;
    /// <summary>Maximum queued callbacks started by one Update. A running callback is not preempted.</summary>
    public int ActionsPerUpdate { get; init; } = 64;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ActionsPerUpdate);
    }
}
