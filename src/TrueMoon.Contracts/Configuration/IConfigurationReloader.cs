namespace TrueMoon.Configuration;

/// <summary>Explicit reload of the App's frozen profile; does not recreate services.</summary>
public interface IConfigurationReloader
{
    /// <summary>Validates and atomically publishes a complete candidate, or retains the current snapshot.</summary>
    /// <param name="cancellationToken">Cancellation before publication.</param><returns>The reload outcome.</returns>
    ValueTask<ConfigurationUpdate> ReloadAsync(CancellationToken cancellationToken = default);
}
