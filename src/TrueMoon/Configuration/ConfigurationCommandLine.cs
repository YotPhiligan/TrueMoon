using System.Globalization;
using System.Text.Json;

namespace TrueMoon.Configuration;

/// <summary>Parses schema-selected command line options without App, DI or IO.</summary>
public static class ConfigurationCommandLine
{
    /// <summary>Captures arguments without the executable at the common OS boundary.</summary>
    /// <returns>A detached argument list.</returns>
    public static IReadOnlyList<string> Capture() => Array.AsReadOnly(Environment.GetCommandLineArgs().Skip(1).ToArray());

    /// <summary>Parses only declared options for bootstrap; other tokens remain positional.</summary>
    /// <param name="arguments">Explicit arguments.</param><param name="keys">Selected field schema.</param>
    /// <returns>Canonical selected entries and unconsumed tokens.</returns>
    public static ConfigurationCommandLineResult ParseKnown(IReadOnlyList<string> arguments, IReadOnlyList<ConfigurationKey> keys)
        => ParseCore(arguments, keys, true);

    /// <summary>Parses a command surface that permits each scalar/flag only once; repeated fields retain their collection semantics.</summary>
    /// <param name="arguments">Explicit arguments.</param><param name="keys">Command schema.</param><returns>Canonical entries and positionals.</returns>
    public static ConfigurationCommandLineResult ParseSingleUse(IReadOnlyList<string> arguments, IReadOnlyList<ConfigurationKey> keys)
        => ParseCore(arguments, keys, false, true);

    /// <summary>Parses arguments excluding the executable.</summary>
    /// <param name="arguments">Explicit arguments.</param><param name="keys">Host field schema.</param>
    /// <returns>Canonical entries and untouched positional arguments.</returns>
    public static ConfigurationCommandLineResult Parse(IReadOnlyList<string> arguments, IReadOnlyList<ConfigurationKey> keys)
        => ParseCore(arguments, keys, false);

    private static ConfigurationCommandLineResult ParseCore(IReadOnlyList<string> arguments, IReadOnlyList<ConfigurationKey> keys, bool allowUnknown, bool singleUse = false)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var map = ConfigurationMetadata.Keys(keys);
        var aliases = new Dictionary<string, ConfigurationKey>(ConfigurationMetadata.Names);
        foreach (var key in map.Values)
            foreach (var alias in new[] { "--" + key.Path }.Concat(key.Field.Aliases ?? [])) aliases.Add(alias, key);
        var values = new Dictionary<string, JsonElement>(ConfigurationMetadata.Names);
        var repeated = new Dictionary<string, List<JsonElement>>(ConfigurationMetadata.Names);
        var positionals = new List<string>();
        var options = true;
        for (var index = 0; index < arguments.Count; index++)
        {
            var token = arguments[index] ?? throw ConfigurationMetadata.Error("InvalidArgument");
            if (options && token == "--") { options = false; continue; }
            if (!options || !token.StartsWith('-') || token == "-") { positionals.Add(token); continue; }
            var separator = token.IndexOf('=');
            var alias = separator < 0 ? token : token[..separator];
            if (!aliases.TryGetValue(alias, out var key))
            {
                if (!allowUnknown) throw ConfigurationMetadata.Error("UnknownOption");
                positionals.Add(token); continue;
            }
            if (singleUse && key.Field.Argument != ConfigurationArgumentKind.Repeated && values.ContainsKey(key.Path))
                throw ConfigurationMetadata.Error("DuplicateOption", key.Path);
            JsonElement value;
            if (key.Field.Argument == ConfigurationArgumentKind.Flag && separator < 0)
                value = JsonSerializer.SerializeToElement(true);
            else
            {
                string text;
                if (separator >= 0) text = token[(separator + 1)..];
                else
                {
                    if (index + 1 >= arguments.Count || arguments[index + 1] is not { } next || next == "--")
                        throw ConfigurationMetadata.Error("MissingArgument", key.Path);
                    var expected = ConfigurationCodecs.CollectionElement(key.ValueType) ?? key.ValueType;
                    if (next.StartsWith('-') && next != "-" &&
                        !(ConfigurationMetadata.Numeric(expected) && double.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                        throw ConfigurationMetadata.Error("MissingArgument", key.Path);
                    text = next; index++;
                }
                value = JsonSerializer.SerializeToElement(text);
            }
            if (key.Field.Argument == ConfigurationArgumentKind.Repeated)
            {
                if (!repeated.TryGetValue(key.Path, out var items)) repeated[key.Path] = items = [];
                items.Add(value);
                values[key.Path] = JsonSerializer.SerializeToElement(items);
            }
            else values[key.Path] = value;
        }
        return new(values.Select(value => new ConfigurationEntry(value.Key, value.Value)).ToArray(), positionals);
    }
}
