namespace TrueMoon.Aether.Rendering.Resources;

/// <summary>Resource ownership/preparation failure categories.</summary>
public enum RenderResourceError
{
    /// <summary>Admission would exceed a count or byte limit.</summary>
    LimitExceeded,
    /// <summary>Foreign session/world/project/build/catalog identity.</summary>
    InvalidIdentity,
    /// <summary>Admission or resource use is closed.</summary>
    Closed,
    /// <summary>Native submission/completion cannot be confirmed.</summary>
    UnknownOutcome,
    /// <summary>The injected backend failed preparation.</summary>
    BackendFailure,
    /// <summary>Invalid, foreign or retired resource handle.</summary>
    StaleHandle,
    /// <summary>World render inputs changed before publication.</summary>
    Conflict,
    /// <summary>Cleanup did not release every owned resource.</summary>
    CleanupFailed
}

/// <summary>Observable resource failure. An error does not imply native ownership was released.</summary>
public sealed class RenderResourceException : Exception
{
    /// <summary>Creates an explicit resource outcome.</summary><param name="error">Stable category.</param><param name="message">Diagnostic message.</param><param name="inner">Original failure.</param>
    public RenderResourceException(RenderResourceError error, string message, Exception? inner = null) : base(message, inner) => Error = error;
    /// <summary>Gets the stable category.</summary>
    public RenderResourceError Error { get; }
}

/// <summary>Factory failure retaining a partially created owner for explicit cleanup.</summary>
public sealed class RenderResourceCreationException : Exception
{
    /// <summary>Transfers failed-creation ownership to the consumer.</summary><param name="cause">Original creation error.</param><param name="cleanupOwner">Actual partial owner.</param>
    public RenderResourceCreationException(Exception cause, IAsyncDisposable cleanupOwner) : base("Resource creation retained partial ownership.", cause)
    { ArgumentNullException.ThrowIfNull(cause); ArgumentNullException.ThrowIfNull(cleanupOwner); CleanupOwner = cleanupOwner; }
    /// <summary>Gets the owner which must be retained until successful disposal.</summary>
    public IAsyncDisposable CleanupOwner { get; }
}

internal static class ResourceGuard
{
    internal static void Require(bool condition, RenderResourceError error, string message)
    { if (!condition) throw new RenderResourceException(error, message); }
}
