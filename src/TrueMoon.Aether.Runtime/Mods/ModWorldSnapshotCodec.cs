using System.Text.Json;
using TrueMoon.Aether.Mods;
using TrueMoon.Aether.Runtime.Scenes;

namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>Strict bounded neutral snapshot envelope using the existing scene codec; no mod assembly or private CLR type is needed.</summary>
public sealed class ModWorldSnapshotCodec(SceneDocumentCodec scenes)
{
    /// <summary>Maximum complete UTF8 snapshot length.</summary>
    public const int MaximumBytes = 16 * 1024 * 1024;
    /// <summary>Validates and encodes neutral state.</summary><param name="snapshot">Immutable source.</param><returns>Detached UTF8 bytes.</returns>
    public byte[] Encode(ModWorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FormatVersion != ModWorldSnapshot.CurrentFormatVersion) throw new InvalidDataException("Unsupported World snapshot format.");
        var scene = scenes.Encode(snapshot.Scene);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WriteNumber("formatVersion", snapshot.FormatVersion); writer.WriteNumber("tickNumber", snapshot.TickNumber);
            writer.WritePropertyName("scene"); writer.WriteRawValue(scene); writer.WriteStartArray("systems");
            foreach (var pair in snapshot.Systems.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject(); writer.WriteString("id", pair.Key); writer.WriteNumber("version", pair.Value.Version);
                writer.WriteBase64String("bytes", pair.Value.Bytes.Span); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        if (stream.Length > MaximumBytes) throw new InvalidDataException("World snapshot byte budget exceeded.");
        return stream.ToArray();
    }
    /// <summary>Decodes strict protocol and scene values without loading mod code.</summary><param name="bytes">Bounded UTF8 input.</param><returns>Detached snapshot.</returns>
    public ModWorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("World snapshot byte budget exceeded.");
        try
        {
            using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 64 });
            var value = document.RootElement; Members(value, "formatVersion", "tickNumber", "scene", "systems");
            if (value.GetProperty("formatVersion").GetInt32() != ModWorldSnapshot.CurrentFormatVersion) throw new InvalidDataException("Unsupported World snapshot format.");
            var systems = new List<KeyValuePair<string, ModSystemState>>();
            foreach (var entry in value.GetProperty("systems").EnumerateArray())
            {
                Members(entry, "id", "version", "bytes");
                if (systems.Count == 128) throw new InvalidDataException("System state count exceeds budget.");
                systems.Add(new(entry.GetProperty("id").GetString()!, new(entry.GetProperty("version").GetInt32(), entry.GetProperty("bytes").GetBytesFromBase64())));
            }
            return new(scenes.Decode(System.Text.Encoding.UTF8.GetBytes(value.GetProperty("scene").GetRawText())), systems, value.GetProperty("tickNumber").GetUInt64());
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or FormatException or KeyNotFoundException or OverflowException)
        { throw new InvalidDataException("Malformed World snapshot."); }
    }
    private static void Members(JsonElement value, params string[] expected)
    {
        var names = value.EnumerateObject().Select(p => p.Name).ToArray();
        if (names.Length != expected.Length || names.Distinct(StringComparer.Ordinal).Count() != names.Length || expected.Any(n => !names.Contains(n, StringComparer.Ordinal)))
            throw new InvalidDataException("Unexpected, duplicate or missing World snapshot member.");
    }
}
