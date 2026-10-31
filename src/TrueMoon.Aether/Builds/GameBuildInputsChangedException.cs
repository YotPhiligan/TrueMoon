namespace TrueMoon.Aether.Builds;

/// <summary>Rejects a build or startup whose evaluated inputs no longer match its captured witness.</summary>
public sealed class GameBuildInputsChangedException : IOException
{
    /// <summary>Creates an observable mismatch without starting a runtime.</summary>
    /// <param name="expected">Fingerprint captured before compilation.</param><param name="actual">Fingerprint observed by re-evaluation.</param>
    public GameBuildInputsChangedException(string expected, string actual)
        : base("Game build inputs changed. Build the current sources again before starting the scene.")
    { Expected = expected; Actual = actual; }
    /// <summary>Original evaluated-input fingerprint.</summary>
    public string Expected { get; }
    /// <summary>Current evaluated-input fingerprint.</summary>
    public string Actual { get; }
}
