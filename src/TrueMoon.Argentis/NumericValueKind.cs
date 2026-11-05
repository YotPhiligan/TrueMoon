namespace TrueMoon.Argentis;

/// <summary>Arithmetic used by an explicitly enabled numeric adjustment.</summary>
public enum NumericValueKind
{
    /// <summary>Finite invariant-culture floating point values.</summary>
    Real,
    /// <summary>Checked signed Int64 arithmetic without a floating point round trip.</summary>
    Integer
}

/// <summary>How a captured numeric gesture ended; no result commits application data.</summary>
public enum NumericGestureResult
{
    /// <summary>The resulting draft was retained.</summary>
    Completed,
    /// <summary>The exact pre-gesture draft was restored.</summary>
    Cancelled,
    /// <summary>An external value/context superseded the gesture; it was not overwritten.</summary>
    Interrupted
}
