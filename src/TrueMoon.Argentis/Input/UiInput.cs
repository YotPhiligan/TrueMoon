namespace TrueMoon.Argentis.Input;

/// <summary>An input event in logical coordinates; primary pointer button is zero.</summary>
public readonly record struct UiInput(InputKind Kind, float X = 0, float Y = 0, UiKey Key = UiKey.None,
    string? Text = null, float WheelDelta = 0, bool Shift = false, bool Control = false, int Button = 0);