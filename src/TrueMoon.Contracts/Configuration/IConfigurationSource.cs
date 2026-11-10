namespace TrueMoon.Configuration;

/// <summary>Loads one data-only configuration source.</summary>
public interface IConfigurationSource
{
    /// <summary>Loads entries without retaining documents or mutable collections.</summary>
    /// <param name="keys">Known keys.</param><param name="cancellationToken">Cancellation.</param><returns>Source entries.</returns>
    ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default);
}
