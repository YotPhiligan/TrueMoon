namespace TrueMoon.Configuration;

internal sealed class BoundConfigurationSnapshot(IConfigurationSnapshot raw,
    IReadOnlyDictionary<object, BoundConfigurationOptions> options,
    IReadOnlyList<ConfigurationObservation> observations, long generation = 1) : IConfigurationSnapshot
{
    public long Generation => generation;
    internal IReadOnlyDictionary<object, BoundConfigurationOptions> Options => options;
    internal IReadOnlyList<ConfigurationObservation> Observations => observations;
    internal BoundConfigurationSnapshot WithGeneration(long value) => new(raw, options, observations, value);
    public ConfigurationRead<T> Read<T>(string path) => raw.Read<T>(path);
    public T GetOptions<T>(IConfigurationSchema<T> schema) where T : class
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (!options.TryGetValue(schema, out var bound) || bound.ValueType != typeof(T))
            throw new ArgumentException("The schema instance is not registered in this snapshot.", nameof(schema));
        return (T)bound.Copy();
    }
}
