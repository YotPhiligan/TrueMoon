namespace TrueMoon.Configuration;

internal sealed class ConfigurationReader(ConfigurationRuntime runtime) : IConfigurationReader, IConfigurationReloader, IConfigurationWriter, IConfigurationChanges
{
    public IConfigurationSnapshot Current => runtime.Current;
    public ValueTask<ConfigurationUpdate> ReloadAsync(CancellationToken cancellationToken = default) => runtime.ReloadAsync(cancellationToken);
    public ValueTask WriteAsync(string sourceId, IReadOnlyList<ConfigurationEdit> edits, CancellationToken cancellationToken = default)
        => runtime.WriteAsync(sourceId, edits, cancellationToken);
    public IDisposable Subscribe(Action<ConfigurationUpdate> observer) => runtime.Subscribe(observer);
}
