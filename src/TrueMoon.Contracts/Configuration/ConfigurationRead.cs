namespace TrueMoon.Configuration;

/// <summary>A typed value and its presence state.</summary>
/// <param name="State">State.</param>
/// <param name="Value">Value.</param>
/// <param name="Origin">Origin.</param>
public sealed record ConfigurationRead<T>(ConfigurationReadState State, T? Value, ConfigurationOrigin? Origin);
