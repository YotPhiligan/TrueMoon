namespace TrueMoon.Configuration;

/// <summary>Optional source capability for explicit patches; existing sources remain read-only.</summary>
public interface IConfigurationMutableSource : IConfigurationSource
{
    /// <summary>Applies already normalized edits, without publishing an App snapshot.</summary>
    /// <param name="keys">Frozen schema keys.</param><param name="edits">Validated edits; null deletes, JSON null stores null.</param>
    /// <param name="cancellationToken">Cancellation before source commit.</param><returns>Write completion.</returns>
    ValueTask WriteAsync(IReadOnlyList<ConfigurationKey> keys, IReadOnlyList<ConfigurationEdit> edits, CancellationToken cancellationToken = default);
}
