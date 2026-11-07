namespace TrueMoon.Aether.Ecs;

/// <summary>Activation and cleanup both failed. The retained owner must be disposed again to finish cleanup.</summary>
public sealed class EcsSchedulerCreationException : Exception
{
    internal EcsSchedulerCreationException(Exception creation, Exception cleanup, EcsScheduler owner)
        : base("System activation and cleanup failed; retry disposal of CleanupOwner.", new AggregateException(creation, cleanup)) => CleanupOwner = owner;
    /// <summary>Gets the partial graph retaining failed resources and its borrowed World lease.</summary>
    public EcsScheduler CleanupOwner { get; }
}
