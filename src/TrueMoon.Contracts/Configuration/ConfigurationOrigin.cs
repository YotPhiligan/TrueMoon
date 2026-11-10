namespace TrueMoon.Configuration;

/// <summary>Identifies the winning source without exposing its value.</summary>
/// <param name="SourceId">SourceId.</param>
/// <param name="Priority">Priority.</param>
/// <param name="Order">Order.</param>
public sealed record ConfigurationOrigin(string SourceId, ConfigurationSourcePriority Priority, int Order);
