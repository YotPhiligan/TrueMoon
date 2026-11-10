namespace TrueMoon.Configuration;

/// <summary>Distinguishes missing, null and non-null values.</summary>
public enum ConfigurationReadState
{
    /// <summary>Missing.</summary>
    Missing,
    /// <summary>Null.</summary>
    Null,
    /// <summary>Value.</summary>
    Value,
}
