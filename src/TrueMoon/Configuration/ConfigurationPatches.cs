using System.Text.Json;

namespace TrueMoon.Configuration;

internal static class ConfigurationPatches
{
    internal static IReadOnlyList<ConfigurationEdit> Copy(IReadOnlyList<ConfigurationEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        try { return Array.AsReadOnly(edits.Select(edit => new ConfigurationEdit(edit.Path, edit.Value)).ToArray()); }
        catch (Exception) { throw ConfigurationMetadata.Error("InvalidEdit"); }
    }

    internal static IReadOnlyList<ConfigurationEdit> Validate(IReadOnlyDictionary<string, ConfigurationKey> keys,
        IReadOnlyList<ConfigurationEdit> edits)
    {
        var result = new List<ConfigurationEdit>();
        var seen = new HashSet<string>(ConfigurationMetadata.Names);
        foreach (var edit in edits)
        {
            if (edit?.Path is null || !keys.TryGetValue(edit.Path, out var key)) throw ConfigurationMetadata.Error("UnknownKey");
            if (!seen.Add(key.Path)) throw ConfigurationMetadata.Error("DuplicateKey", key.Path);
            if (edit.Value is { } value)
            {
                ConfigurationMetadata.ValidateValue(key, value);
                // Serialize the whole patch before opening any source, including custom codecs' inputs.
                try { _ = JsonSerializer.SerializeToUtf8Bytes(value); }
                catch (Exception) { throw ConfigurationMetadata.Error("InvalidValue", key.Path); }
            }
            result.Add(new(key.Path, edit.Value));
        }
        return result.AsReadOnly();
    }
}
