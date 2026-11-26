namespace TrueMoon.Argentis.Input;

/// <summary>Services supplied by a session while routing input.</summary>
public interface IInputContext
{
    /// <summary>Gets the eligible keyboard focus owner in this session, or <see langword="null"/>.</summary>
    Element? FocusedElement { get; }
    /// <summary>Moves keyboard focus if the target accepts input in this session.</summary>
    /// <param name="element">The requested focus target.</param>
    /// <returns><see langword="false"/> if the target is unavailable; existing focus is unchanged.</returns>
    bool TryFocus(Element element);
    /// <summary>Moves keyboard focus.</summary>
    void Focus(Element element);
    /// <summary>Routes subsequent pointer events to this element until release.</summary>
    void Capture(Element element);
    /// <summary>Releases pointer capture.</summary>
    void ReleaseCapture(Element element);
    /// <summary>Reads clipboard text, or null when no text format is available. Operational errors propagate to the session.</summary>
    string? GetClipboardText();
    /// <summary>Writes clipboard text when supported.</summary>
    void SetClipboardText(string text);
}