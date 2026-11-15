namespace TrueMoon.Aether.Kul;

/// <summary>End-of-tick sample, not a realtime device callback. Runtime-only and reference-free.</summary>
/// <param name="Tick">Last successful audio tick.</param><param name="Sequence">Last accepted command.</param>
/// <param name="Phase">Observed state.</param><param name="CursorFrames">Native sample-frame cursor.</param>
public readonly record struct AudioSourceState(ulong Tick, ulong Sequence, AudioSourcePhase Phase, ulong CursorFrames);
