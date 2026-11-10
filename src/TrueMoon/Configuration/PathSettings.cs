namespace TrueMoon.Configuration;

/// <summary>Data-only startup settings for application folders.</summary>
public sealed record PathSettings
{
    /// <summary>Explicit product root, or null for the OS/company/product default.</summary>
    public string? Root { get; set; }
}
