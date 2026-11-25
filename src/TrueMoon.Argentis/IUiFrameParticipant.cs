using TrueMoon.Argentis.Input;

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