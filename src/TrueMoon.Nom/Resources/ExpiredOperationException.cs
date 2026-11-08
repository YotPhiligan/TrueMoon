namespace TrueMoon.Nom.Resources;

/// <summary>The journal outcome was pruned; the old ticket cannot execute again.</summary>
public sealed class ExpiredOperationException() : InvalidOperationException("Operation outcome expired; token cannot execute again.");
