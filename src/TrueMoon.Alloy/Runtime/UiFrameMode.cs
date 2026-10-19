namespace TrueMoon.Alloy;

/// <summary>Controls hosted frame polling independently of tree invalidation.</summary>
public enum UiFrameMode
{
    /// <summary>On-demand without FramePresented observers; continuous while observers are attached for compatibility.</summary>
    Automatic,
    /// <summary>Update/present only for work, invalidation, explicit requests or native refresh. Observers do not enable polling.</summary>
    OnDemand,
    /// <summary>Poll Update and present at the configured frame rate, including clean trees. Use RequestFrame for dynamic redraw.</summary>
    Continuous
}
