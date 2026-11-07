namespace TrueMoon.Aether.Ecs.Runtime;

/// <summary>A preparation failed and retains an owned controller/graph requiring cleanup retry.</summary>
public sealed class SceneWorldPreparationException : Exception
{
    /// <summary>Preserves an original preparation/cleanup error and its exclusively owned unfinished resource.</summary>
    /// <param name="error">Original failure, including cleanup details.</param><param name="cleanupOwner">Owner to dispose before its borrowed scene.</param>
    public SceneWorldPreparationException(Exception error, IDisposable cleanupOwner)
        : base("Scene preparation and cleanup failed; retry disposal of CleanupOwner.", error)
    { ArgumentNullException.ThrowIfNull(error); ArgumentNullException.ThrowIfNull(cleanupOwner); CleanupOwner = cleanupOwner; }
    /// <summary>Gets the controller retaining failed cleanup and borrowed World leases.</summary>
    public IDisposable CleanupOwner { get; }
}
