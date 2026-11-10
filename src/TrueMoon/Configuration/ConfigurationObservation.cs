using System.Reflection;
using System.Text.Json;

namespace TrueMoon.Configuration;

internal sealed record ConfigurationObservation(ConfigurationReadState State, ConfigurationOrigin? Origin, JsonElement? Value)
{
    internal bool Same(ConfigurationObservation other) => State == other.State && Origin == other.Origin &&
        (Value is { } left ? other.Value is { } right && JsonElement.DeepEquals(left, right) : other.Value is null);

    internal static Func<IConfigurationSnapshot, ConfigurationObservation> Reader(ConfigurationKey key)
    {
        // Close each typed reader once at composition, never on Current/Read/GetOptions.
        var read = typeof(ConfigurationObservation).GetMethod(nameof(Read), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(key.ValueType).CreateDelegate<Func<ConfigurationKey, IConfigurationSnapshot, ConfigurationObservation>>();
        return snapshot => read(key, snapshot);
    }

    private static ConfigurationObservation Read<T>(ConfigurationKey key, IConfigurationSnapshot snapshot)
    {
        try
        {
            var read = snapshot.Read<T>(key.Path);
            if (!Enum.IsDefined(read.State)) throw ConfigurationMetadata.Error("InvalidValue", key.Path);
            JsonElement? value = read.State == ConfigurationReadState.Missing ? null :
                (key.Codec?.Write(read.Value) ?? JsonSerializer.SerializeToElement(read.Value)).Clone();
            if (value is { ValueKind: JsonValueKind.Undefined }) throw ConfigurationMetadata.Error("InvalidValue", key.Path);
            return new(read.State, read.Origin, value);
        }
        catch (Exception) { throw ConfigurationMetadata.Error("InvalidValue", key.Path); }
    }
}
