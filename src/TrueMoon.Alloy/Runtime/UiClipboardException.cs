namespace TrueMoon.Alloy;

/// <summary>The clipboard operation that could not complete.</summary>
public enum UiClipboardOperation
{
    /// <summary>Reading text.</summary>
    Read,
    /// <summary>Replacing clipboard contents with text.</summary>
    Write
}

/// <summary>An operational clipboard failure. Routed input reports it without cancelling the UI session.</summary>
public sealed class UiClipboardException : Exception
{
    /// <summary>The failed operation.</summary>
    public UiClipboardOperation Operation { get; }
    /// <summary>Creates a failure while preserving the platform error and cleanup errors.</summary>
    /// <param name="operation">The failed operation.</param>
    /// <param name="cause">The original platform failure.</param>
    public UiClipboardException(UiClipboardOperation operation, Exception cause)
        : base($"Clipboard {operation} failed.", cause ?? throw new ArgumentNullException(nameof(cause)))
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        Operation = operation;
    }
}
