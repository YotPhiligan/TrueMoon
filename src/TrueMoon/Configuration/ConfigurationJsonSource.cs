using System.Text.Json;
using System.Text.Json.Nodes;

namespace TrueMoon.Configuration;

internal sealed class ConfigurationJsonSource : IConfigurationMutableSource
{
    private readonly string _path;
    private readonly bool _optional;
    internal ConfigurationJsonSource(string path, bool optional)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Configuration file path must be absolute.", nameof(path));
        _path = Path.GetFullPath(path); _optional = optional;
    }

    public async ValueTask WriteAsync(IReadOnlyList<ConfigurationKey> keys, IReadOnlyList<ConfigurationEdit> edits,
        CancellationToken cancellationToken = default)
    {
        var patch = ConfigurationPatches.Validate(ConfigurationMetadata.Keys(keys), ConfigurationPatches.Copy(edits));
        cancellationToken.ThrowIfCancellationRequested();
        if (patch.Count == 0) return;
        var entries = (await ReadAsync(keys, cancellationToken).ConfigureAwait(false))
            .ToDictionary(entry => entry.Path, entry => entry.Value, ConfigurationMetadata.Names);
        foreach (var edit in patch)
            if (edit.Value is { } value) entries[edit.Path] = value;
            else entries.Remove(edit.Path);
        var root = new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        foreach (var entry in entries.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var parts = entry.Key.Split(':');
            var parent = root;
            foreach (var part in parts[..^1])
            {
                if (parent[part] is not JsonObject child) parent[part] = child = new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true });
                parent = child;
            }
            parent[parts[^1]] = JsonNode.Parse(entry.Value.GetRawText());
        }
        var text = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        // Stage beside the destination; never truncate it before a complete patch has been written.
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, text, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, _path, true);
            // The rename committed: do not report later cancellation as a canceled write.
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception) { /* Cleanup must not replace a value-safe write failure. */ }
        }
    }

    public async ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
    {
        string text;
        try { text = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false); }
        catch (FileNotFoundException) when (_optional) { return []; }
        catch (DirectoryNotFoundException) when (_optional) { return []; }
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw ConfigurationMetadata.Error("JsonRoot");
        var map = ConfigurationMetadata.Keys(keys);
        var result = new List<ConfigurationEntry>();
        Visit(document.RootElement, "");
        return result.AsReadOnly();

        void Visit(JsonElement element, string parent)
        {
            var names = new HashSet<string>(ConfigurationMetadata.Names);
            foreach (var property in element.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(property.Name) || property.Name.Contains(':') || property.Name.Any(char.IsControl))
                    throw ConfigurationMetadata.Error("JsonKey");
                if (!names.Add(property.Name)) throw ConfigurationMetadata.Error("DuplicateJsonKey");
                var path = parent.Length == 0 ? property.Name : parent + ":" + property.Name;
                if (property.Value.ValueKind == JsonValueKind.Object && !map.ContainsKey(path) && property.Value.EnumerateObject().Any())
                    Visit(property.Value, path);
                else
                {
                    if (!map.TryGetValue(path, out var key)) throw ConfigurationMetadata.Error("UnknownJsonKey");
                    result.Add(new(key.Path, property.Value));
                }
            }
        }
    }
}
