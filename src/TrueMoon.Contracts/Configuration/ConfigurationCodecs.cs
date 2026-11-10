using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace TrueMoon.Configuration;

internal static class ConfigurationCodecs
{
    internal static bool Supported(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(string) || type == typeof(bool) || type.IsEnum || ConfigurationMetadata.Numeric(type) ||
            type == typeof(Guid) || type == typeof(TimeSpan) || CollectionElement(type) is { } element && Supported(element);
    }
    internal static object? Read(Type declared, JsonElement value)
    {
        var type = Nullable.GetUnderlyingType(declared) ?? declared;
        if (value.ValueKind == JsonValueKind.Null)
        {
            if (declared.IsValueType && Nullable.GetUnderlyingType(declared) is null) throw new FormatException();
            return null;
        }
        if (type == typeof(string))
            return value.ValueKind == JsonValueKind.String ? value.GetString() : throw new FormatException();
        if (CollectionElement(type) is { } element)
        {
            if (value.ValueKind != JsonValueKind.Array) throw new FormatException();
            var array = Array.CreateInstance(element, value.GetArrayLength());
            var index = 0;
            foreach (var item in value.EnumerateArray()) array.SetValue(Read(element, item), index++);
            if (type.IsAssignableFrom(array.GetType())) return array;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)Activator.CreateInstance(type)!;
                foreach (var item in array) list.Add(item);
                return list;
            }
            throw new NotSupportedException();
        }
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        if (type == typeof(bool)) return bool.Parse(text);
        if (type.IsEnum)
        {
            var result = Enum.Parse(type, text, true);
            if (!Enum.IsDefined(type, result)) throw new FormatException();
            return result;
        }
        if (ConfigurationMetadata.Numeric(type) || type == typeof(Guid) || type == typeof(TimeSpan))
        {
            var converter = TypeDescriptor.GetConverter(type);
            return converter.ConvertFrom(null, CultureInfo.InvariantCulture, text);
        }
        throw new NotSupportedException("An explicit codec is required.");
    }

    internal static Type? CollectionElement(Type type)
    {
        if (type.IsArray && type.GetArrayRank() == 1) return type.GetElementType();
        if (!type.IsGenericType) return null;
        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(IReadOnlyList<>) || definition == typeof(IReadOnlyCollection<>) ||
            definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>) || definition == typeof(IList<>) ||
            definition == typeof(List<>) ? type.GetGenericArguments()[0] : null;
    }

    internal static IConfigurationValueCodec Box<T>(IConfigurationValueCodec<T>? codec = null) => new Boxed<T>(codec);

    private sealed class Boxed<T>(IConfigurationValueCodec<T>? codec) : IConfigurationValueCodec
    {
        public Type ValueType => typeof(T);
        public object? Read(JsonElement value) => codec is null ? ConfigurationCodecs.Read(typeof(T), value) : codec.Read(value);
        public JsonElement Write(object? value) => codec is null
            ? JsonSerializer.SerializeToElement((T?)value)
            : codec.Write((T)value!).Clone();
    }
}
