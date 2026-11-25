namespace TrueMoon.Argentis.Editors;

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