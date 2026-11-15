namespace TrueMoon.Kul;

/// <summary>Value settings captured when a voice is admitted.</summary>
public sealed record AudioPlayOptions
{
    /// <summary>Linear gain, between zero and one.</summary>
    public float Volume { get; init; } = 1;
    /// <summary>Restarts the decoded clip or stream at EOF.</summary>
    public bool Loop { get; init; }
    /// <summary>Prepares a voice without starting it. No sample can play before an explicit Play call.</summary>
    public bool StartPaused { get; init; }
    /// <summary>Excludes this reusable voice from EOF collection, including automatic admission cleanup. Dispose explicitly.</summary>
    public bool RetainAtEnd { get; init; }
}
