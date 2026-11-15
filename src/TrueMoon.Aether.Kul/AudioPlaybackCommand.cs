namespace TrueMoon.Aether.Kul;

/// <summary>Sequence increases per attached entity generation; an identical sequence is an idempotent replay only with identical payload.</summary>
/// <param name="Sequence">Positive monotonic sequence.</param><param name="Kind">Operation.</param>
/// <param name="Frame">Seek destination; zero for other operations.</param>
public readonly record struct AudioPlaybackCommand(ulong Sequence, AudioCommandKind Kind, ulong Frame = 0);
