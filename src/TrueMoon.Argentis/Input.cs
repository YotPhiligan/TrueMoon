namespace TrueMoon.Argentis;

/// <summary>Normalized input independent of any windowing library.</summary>
public enum InputKind { PointerMove, PointerDown, PointerUp, Wheel, KeyDown, KeyUp, Text, FocusLost }
/// <summary>Keys used by the built-in controls.</summary>
public enum UiKey { None, Tab, Enter, Space, Escape, Left, Right, Home, End, Backspace, Delete, A, C, V, X }
/// <summary>An input event in logical coordinates; primary pointer button is zero.</summary>
public readonly record struct UiInput(InputKind Kind, float X = 0, float Y = 0, UiKey Key = UiKey.None,
    string? Text = null, float WheelDelta = 0, bool Shift = false, bool Control = false, int Button = 0);
/// <summary>Services supplied by a session while routing input.</summary>
public interface IInputContext
{
    /// <summary>Moves keyboard focus.</summary>
    void Focus(Element element);
    /// <summary>Routes subsequent pointer events to this element until release.</summary>
    void Capture(Element element);
    /// <summary>Releases pointer capture.</summary>
    void ReleaseCapture(Element element);
    /// <summary>Reads clipboard text, or null when unavailable.</summary>
    string? GetClipboardText();
    /// <summary>Writes clipboard text when supported.</summary>
    void SetClipboardText(string text);
}
