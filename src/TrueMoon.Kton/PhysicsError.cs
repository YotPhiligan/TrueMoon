namespace TrueMoon.Kton;

/// <summary>Stable categories for validation and provider failures.</summary>
public enum PhysicsErrorCode
{
    /// <summary>No error.</summary>
    None,
    /// <summary>Nonfinite, out-of-range or inconsistent input.</summary>
    InvalidArgument,
    /// <summary>Default or structurally invalid runtime ID.</summary>
    InvalidIdentifier,
    /// <summary>ID issued by another world incarnation.</summary>
    ForeignWorld,
    /// <summary>Slot absent, retired or issued with another generation.</summary>
    StaleIdentifier,
    /// <summary>Admission capacity exceeded before mutation.</summary>
    CapacityExceeded,
    /// <summary>Shape or shape/body combination not supported.</summary>
    UnsupportedGeometry,
    /// <summary>Quality profile or operation unavailable.</summary>
    UnsupportedProfile,
    /// <summary>Body is still referenced by a joint, or shape by a body/query operation.</summary>
    ResourceInUse,
    /// <summary>World invocation is concurrent or reentrant.</summary>
    Busy,
    /// <summary>World has been disposed.</summary>
    Disposed,
    /// <summary>World cannot continue after partial simulation failure.</summary>
    Faulted,
    /// <summary>Gameplay contact buffer overflow after a begun step; world becomes Faulted.</summary>
    ContactOverflow,
    /// <summary>Owned query scratch limit exceeded; do not silently allocate without a bound.</summary>
    ScratchLimitExceeded,
    /// <summary>Tick is not exactly the next tick, or the tick counter is exhausted.</summary>
    InvalidTick
}

/// <summary>Allocation-free successful validation; a failure names its input field.</summary>
/// <param name="Code">Error category.</param>
/// <param name="Field">Stable field path, without sensitive input contents.</param>
public readonly record struct PhysicsError(PhysicsErrorCode Code, string? Field = null)
{
    /// <summary>Whether this value describes failure.</summary>
    public bool IsFailure => Code != PhysicsErrorCode.None;

    /// <summary>Throws a typed exception for failure, otherwise returns.</summary>
    public void ThrowIfFailure()
    {
        if (IsFailure) throw new PhysicsException(this);
    }
}

/// <summary>Typed provider/validation failure. Step failures may leave a Faulted world; no rollback is implied.</summary>
public sealed class PhysicsException : Exception
{
    /// <summary>Creates an exception for a failure.</summary>
    /// <param name="error">Non-success error.</param>
    /// <param name="innerException">Optional underlying failure.</param>
    public PhysicsException(PhysicsError error, Exception? innerException = null)
        : base($"Physics operation failed: {error.Code} ({error.Field}).", innerException)
    {
        if (!error.IsFailure) throw new ArgumentException("A failure is required.", nameof(error));
        Error = error;
    }

    /// <summary>Stable error category and field.</summary>
    public PhysicsError Error { get; }
}
