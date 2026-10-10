using System.Text.Json;

namespace TrueMoon.Configuration;

/// <summary>A JSON object stored in a file. Set persists changes synchronously.</summary>
public class JsonConfigurationSection : ConfigurableBase, IConfigurationSection, IConfigurationFileHandle
{
    private readonly string _filePath;
    private readonly Lock _fileLock = new();

    public JsonConfigurationSection(string filePath) : base(Read(filePath))
    {
        _filePath = Path.GetFullPath(filePath);
        Name = Path.GetFileNameWithoutExtension(_filePath);
    }

    private static Dictionary<string, object?> Read(string filePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(filePath));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException($"Configuration file '{filePath}' must contain a JSON object.");
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name,
            property => property.Value.ValueKind == JsonValueKind.Null ? null : (object?)property.Value.Clone());
    }

    public override void Set<T>(string key, T? value) where T : default
    {
        lock (_fileLock)
        {
            // Serialize before updating storage; invalid values and failed writes leave the snapshot intact.
            var entries = GetList().ToDictionary(entry => entry.key, entry => entry.value);
            entries[key] = value;
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
            base.Set(key, value);
        }
    }

    public string Name { get; }
    public FileInfo GetFileInfo() => new(_filePath);
}
