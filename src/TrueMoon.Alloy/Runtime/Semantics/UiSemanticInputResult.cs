namespace TrueMoon.Alloy.Runtime.Semantics;

/// <summary>One guarded input event and the resulting session input state.</summary>
/// <param name="Status">Routing or refusal outcome.</param>
/// <param name="HitTargetId">Nearest semantic ID of the routing origin, the blocking element or consuming observer, when present.</param>
/// <param name="FocusedId">Nearest semantic ID of the keyboard focus after the event, when present.</param>
/// <param name="CapturedId">Nearest semantic ID of the pointer capture after the event, when present.</param>
/// <param name="Input">The normal session result. A refused event has Handled set to false.</param>
public readonly record struct UiSemanticInputResult(UiSemanticTargetStatus Status, string? HitTargetId,
    string? FocusedId, string? CapturedId, UiInputResult Input);
