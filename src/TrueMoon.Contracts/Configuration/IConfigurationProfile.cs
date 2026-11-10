namespace TrueMoon.Configuration;

/// <summary>A frozen data-only configuration plan.</summary>
public interface IConfigurationProfile
{
    /// <summary>Loads an independent generation-one snapshot.</summary><param name="cancellationToken">Cancellation.</param><returns>Validated raw values.</returns>
    ValueTask<IConfigurationSnapshot> LoadAsync(CancellationToken cancellationToken = default);
}
