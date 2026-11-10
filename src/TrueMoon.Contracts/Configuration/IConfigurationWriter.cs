namespace TrueMoon.Configuration;

/// <summary>Explicit source writes, serialized with App reload. Writing does not publish a snapshot.</summary>
public interface IConfigurationWriter
{
    /// <summary>Validates and applies edits to one mutable source.</summary>
    /// <param name="sourceId">Exact registered source identifier.</param><param name="edits">Edits to copy and validate before IO.</param>
    /// <param name="cancellationToken">Cancellation before write commit.</param><returns>Write completion.</returns>
    ValueTask WriteAsync(string sourceId, IReadOnlyList<ConfigurationEdit> edits, CancellationToken cancellationToken = default);
}
