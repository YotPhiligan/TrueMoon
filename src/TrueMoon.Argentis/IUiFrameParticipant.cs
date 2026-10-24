namespace TrueMoon.Argentis;

/// <summary>A retained control that needs owner-thread frame ticks only while a transition or delay is pending.</summary>
public interface IUiFrameParticipant
{
    /// <summary>Gets whether another paced frame is required.</summary>
    bool NeedsFrame { get; }
    /// <summary>Advances transient presentation using monotonic elapsed time; never runs on a worker.</summary>
    /// <param name="elapsed">Time since the preceding owner update.</param><param name="context">Borrowed input/focus services for this call.</param>
    void AdvanceFrame(TimeSpan elapsed, IInputContext context);
}

/// <summary>Observes input before focus/capture routing, for menus, overlays and global control activation.</summary>
public interface IUiInputObserver
{
    /// <summary>Higher values run first, allowing a popup owner to handle keyboard dismissal before its host.</summary>
    int InputPriority => 0;
    /// <summary>Observes normalized input. True consumes it before underlying controls can act.</summary>
    /// <param name="input">Current input.</param><param name="context">Borrowed owner-thread services.</param><returns>Whether consumed.</returns>
    bool PreviewInput(UiInput input, IInputContext context);
}
