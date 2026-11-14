namespace TrueMoon.Kul;

/// <summary>Value settings captured when a voice is admitted.</summary>
public sealed record AudioPlayOptions
{
    /// <summary>Linear gain, between zero and one.</summary>
    public float Volume { get; init; } = 1;
    /// <summary>Restarts the decoded clip or stream at EOF.</summary>
    public bool Loop { get; init; }
}
