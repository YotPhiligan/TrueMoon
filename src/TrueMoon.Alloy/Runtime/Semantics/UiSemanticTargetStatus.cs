namespace TrueMoon.Alloy.Runtime.Semantics;

/// <summary>Outcome of semantic target lookup or routed input in a UI session.</summary>
public enum UiSemanticTargetStatus
{
    /// <summary>Lookup found a visible, enabled element in the current tree.</summary>
    Ready,
    /// <summary>Input entered the normal route from the requested target or its descendant.</summary>
    Routed,
    /// <summary>The snapshot belongs to a different session or a superseded tree or layout.</summary>
    StaleSnapshot,
    /// <summary>The snapshot omitted part of the tree, so target identity cannot be confirmed.</summary>
    IncompleteSnapshot,
    /// <summary>No target with this ID was present in the snapshot and live tree.</summary>
    NotFound,
    /// <summary>The target ID identifies more than one node.</summary>
    Ambiguous,
    /// <summary>The target or an ancestor is hidden.</summary>
    Hidden,
    /// <summary>The target or an ancestor is disabled or temporarily excluded from input.</summary>
    Disabled,
    /// <summary>The requested point is outside the target's clipped bounds.</summary>
    OutsideTarget,
    /// <summary>Hit testing selected a different element at the requested point.</summary>
    Obstructed,
    /// <summary>A different element owns pointer capture.</summary>
    PointerCaptured,
    /// <summary>Keyboard input requires focus within the target.</summary>
    NotFocused,
    /// <summary>An input observer consumed the event before the ordinary route.</summary>
    Intercepted,
    /// <summary>The input kind is session-wide or unknown and cannot be sent to an ID.</summary>
    UnsupportedInput
}
