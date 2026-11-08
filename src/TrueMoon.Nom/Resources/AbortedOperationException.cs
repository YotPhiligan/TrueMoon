namespace TrueMoon.Nom.Resources;

/// <summary>The operation was aborted and cannot execute again.</summary>
public sealed class AbortedOperationException() : InvalidOperationException("Operation was aborted; token cannot execute again.");
