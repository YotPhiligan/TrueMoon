using System.Text.Json;

namespace TrueMoon.Configuration;

internal sealed class ConfigurationSnapshot(
    IReadOnlyDictionary<string, ConfigurationKey> keys,
    IReadOnlyDictionary<string, (JsonElement Value, ConfigurationOrigin Origin)> values) : IConfigurationSnapshot
{
    public long Generation => 1;

    public ConfigurationRead<T> Read<T>(string path)
    {
        ConfigurationMetadata.Path(path);
        if (!keys.TryGetValue(path, out var key)) return new(ConfigurationReadState.Missing, default, null);
        if (key.ValueType != typeof(T)) throw ConfigurationMetadata.Error("TypeMismatch", key.Path);
        if (!values.TryGetValue(path, out var entry)) return new(ConfigurationReadState.Missing, default, null);
        try
        {
            var value = key.Codec is { } codec ? codec.Read(entry.Value) : ConfigurationCodecs.Read(typeof(T), entry.Value);
            return new(entry.Value.ValueKind == JsonValueKind.Null ? ConfigurationReadState.Null : ConfigurationReadState.Value,
                (T?)value, entry.Origin);
        }
        catch (Exception)
        { throw ConfigurationMetadata.Error("InvalidValue", key.Path, entry.Origin.SourceId); }
    }

    public T GetOptions<T>(IConfigurationSchema<T> schema) where T : class
    {
        ArgumentNullException.ThrowIfNull(schema);
        throw new InvalidOperationException("A standalone snapshot has no App stage options. Use schema.Bind.");
    }
}
