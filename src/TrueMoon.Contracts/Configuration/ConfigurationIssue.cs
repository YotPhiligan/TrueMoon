namespace TrueMoon.Configuration;

/// <summary>A configuration error without raw input values.</summary>
/// <param name="Code">Code.</param>
/// <param name="Path">Path.</param>
/// <param name="SourceId">SourceId.</param>
/// <param name="Message">Message.</param>
public sealed record ConfigurationIssue(string Code, string? Path, string? SourceId, string Message);
