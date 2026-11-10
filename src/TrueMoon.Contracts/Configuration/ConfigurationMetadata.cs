using System.Collections;
using System.Text.Json;

namespace TrueMoon.Configuration;

internal static class ConfigurationMetadata
{
    internal static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;

    internal static string Path(string path, string parameter = "path")
    {
        ArgumentNullException.ThrowIfNull(path, parameter);
        if (path.Split(':').Any(string.IsNullOrWhiteSpace) || path.Any(char.IsControl))
            throw new ArgumentException("A configuration path needs nonempty segments.", parameter);
        return path;
    }

    internal static Dictionary<string, ConfigurationKey> Keys(IReadOnlyList<ConfigurationKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var result = new Dictionary<string, ConfigurationKey>(Names);
        var aliases = new HashSet<string>(Names);
        foreach (var key in keys)
        {
            ArgumentNullException.ThrowIfNull(key);
            Path(key.Path);
            ArgumentNullException.ThrowIfNull(key.ValueType);
            ArgumentNullException.ThrowIfNull(key.Field);
            if (!Enum.IsDefined(key.Field.Change) || !Enum.IsDefined(key.Field.Argument))
                throw new ArgumentException("Unknown field metadata.", nameof(keys));
            if (key.Codec is { } codec && codec.ValueType != key.ValueType)
                throw new ArgumentException("Codec type differs from field type.", nameof(keys));
            var type = Nullable.GetUnderlyingType(key.ValueType) ?? key.ValueType;
            if (key.Field.Argument == ConfigurationArgumentKind.Flag && type != typeof(bool))
                throw new ArgumentException("A flag must be Boolean.", nameof(keys));
            if (key.Field.Argument == ConfigurationArgumentKind.Repeated &&
                (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type)))
                throw new ArgumentException("A repeated option must be a collection.", nameof(keys));
            if (!result.TryAdd(key.Path, key)) throw new ArgumentException("Duplicate configuration key.", nameof(keys));
            foreach (var alias in new[] { "--" + key.Path }.Concat(key.Field.Aliases ?? []))
            {
                if (string.IsNullOrWhiteSpace(alias) || alias is "-" or "--" || !alias.StartsWith('-') ||
                    alias.Contains('=') || alias.Any(char.IsWhiteSpace) || !aliases.Add(alias))
                    throw new ArgumentException("Invalid or duplicate configuration alias.", nameof(keys));
            }
        }
        foreach (var path in result.Keys)
            for (var separator = path.IndexOf(':'); separator >= 0; separator = path.IndexOf(':', separator + 1))
                if (result.ContainsKey(path[..separator]))
                    throw new ArgumentException("A field cannot also be a parent section.", nameof(keys));
        return result;
    }

    internal static ConfigurationException Error(string code, string? path = null, string? source = null)
        => new([new(code, path, source, "Configuration input is invalid (" + code + ").")]);

    internal static ConfigurationException SourceError(ConfigurationException error,
        IReadOnlyDictionary<string, ConfigurationKey> keys, string source)
    {
        string[] known = ["UnknownKey", "DuplicateKey", "UndefinedValue", "UnknownOption", "InvalidArgument",
            "MissingArgument", "UnexpectedPositional", "UnknownEnvironmentKey", "DuplicateEnvironmentKey",
            "JsonRoot", "JsonKey", "DuplicateJsonKey", "UnknownJsonKey"];
        return new(error.Issues.Select(issue => new ConfigurationIssue(
            known.Contains(issue.Code, StringComparer.Ordinal) ? issue.Code : "SourceRead",
            issue.Path is { } path && keys.TryGetValue(path, out var key) ? key.Path : null,
            source, "Configuration source could not be loaded.")).ToArray());
    }

    internal static ConfigurationException BindingError(ConfigurationException error,
        IReadOnlyList<ConfigurationKey> keys, string section)
    {
        var known = keys.ToDictionary(key => key.Path, Names);
        string[] codes = ["InvalidValue", "TypeMismatch", "RequiredValue", "OptionsValidation"];
        return new(error.Issues.Select(issue => new ConfigurationIssue(
            codes.Contains(issue.Code, StringComparer.Ordinal) ? issue.Code : "OptionsValidation",
            issue.Path is { } path && known.TryGetValue(path, out var key) ? key.Path : section,
            null, "Configuration options could not be validated.")).ToArray());
    }

    internal static bool Numeric(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return Type.GetTypeCode(type) is TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16
            or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single
            or TypeCode.Double or TypeCode.Decimal;
    }

    internal static void ValidateValue(ConfigurationKey key, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Undefined) throw Error("UndefinedValue", key.Path);
        try
        {
            var converted = key.Codec?.Read(value) ?? (key.Codec is null ? ConfigurationCodecs.Read(key.ValueType, value) : null);
            if (converted is null && key.ValueType.IsValueType && Nullable.GetUnderlyingType(key.ValueType) is null ||
                converted is not null && !key.ValueType.IsInstanceOfType(converted))
                throw Error("InvalidValue", key.Path);
        }
        catch (Exception)
        { throw Error("InvalidValue", key.Path); }
    }
}
