using System.Runtime.ExceptionServices;

namespace TrueMoon.Alloy.Platform.Silk;

// Native-origin handlers must return normally. The owner observes the first cause
// after dispatch, then releases the session/input/hooks before destroying the HWND.
internal sealed class WindowCallbackBoundary(Action? verify = null)
{
    private ExceptionDispatchInfo? _failure;

    internal void Execute(Action operation)
    {
        if (_failure != null) return;
        try { verify?.Invoke(); operation(); }
        catch (Exception error) { _failure ??= ExceptionDispatchInfo.Capture(error); }
    }

    internal void Verify() => _failure?.Throw();
}
