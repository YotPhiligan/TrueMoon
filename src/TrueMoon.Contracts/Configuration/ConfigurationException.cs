namespace TrueMoon.Configuration;

/// <summary>A value-safe configuration failure.</summary>
public sealed class ConfigurationException : Exception
{
    /// <summary>Copies diagnostic issues without retaining source exceptions.</summary>
    /// <param name="issues">Value-safe issues.</param>
    public ConfigurationException(IReadOnlyList<ConfigurationIssue> issues) : base("Configuration is invalid.")
    {
        ArgumentNullException.ThrowIfNull(issues);
        var copy = issues.ToArray();
        if (copy.Length == 0 || copy.Any(issue => issue is null)) throw new ArgumentException("At least one non-null issue is required.", nameof(issues));
        Issues = Array.AsReadOnly(copy);
    }
    /// <summary>Diagnostic issues. Values are never included by framework providers.</summary>
    public IReadOnlyList<ConfigurationIssue> Issues { get; }
}
