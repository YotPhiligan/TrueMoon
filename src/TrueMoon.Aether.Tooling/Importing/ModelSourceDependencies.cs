using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace TrueMoon.Aether.Tooling.Importing;

internal readonly record struct ModelSourceDependency(string Name, int MinimumBytes, bool Material = false);

internal static class ModelSourceDependencies
{
    internal static IReadOnlyList<ModelSourceDependency> Discover(CapturedModelFile root, CancellationToken token)
    {
        var extension = Path.GetExtension(root.Name);
        if (extension.Equals(".obj", StringComparison.OrdinalIgnoreCase)) return DiscoverText(root, material: false, token);
        try
        {
            var json = root.Bytes; int? binLength = null;
            if (extension.Equals(".glb", StringComparison.OrdinalIgnoreCase)) (json, binLength) = Glb(root.Bytes);
            ValidateJson(json.Span, token);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            var source = document.RootElement;
            if (source.GetProperty("asset").GetProperty("version").GetString() != "2.0")
                throw new NotSupportedException("Source capture supports glTF asset version 2.0 only.");
            if (source.GetProperty("asset").TryGetProperty("minVersion", out var minimum) && minimum.GetString() != "2.0")
                throw new NotSupportedException("The glTF minimum version is unsupported.");
            foreach (var feature in new[] { "extensionsUsed", "extensionsRequired" })
                if (source.TryGetProperty(feature, out var values) && values.GetArrayLength() != 0)
                    throw new NotSupportedException("Source capture does not support glTF extensions.");
            var dependencies = new Dictionary<string, ModelSourceDependency>(StringComparer.OrdinalIgnoreCase);
            if (GltfBaseColorTexture.Read(source, root.Name) is { } image)
                Add(dependencies, new(image.Name, 33), root.Name);
            if (!source.TryGetProperty("buffers", out var buffers)) return dependencies.Values.ToArray();
            var index = 0;
            foreach (var buffer in buffers.EnumerateArray())
            {
                token.ThrowIfCancellationRequested();
                var length = buffer.GetProperty("byteLength").GetInt32();
                if (length <= 0 || length > 16 * 1024 * 1024) throw new InvalidDataException("Invalid glTF buffer length or profile budget exceeded.");
                if (buffer.TryGetProperty("uri", out var uri))
                    Add(dependencies, new(ModelSourcePath.Resolve(root.Name, uri.GetString()!), length), root.Name);
                else if (index != 0 || binLength == null || binLength < length || binLength - length > 3)
                    throw new InvalidDataException("A URI-less buffer requires the first GLB BIN chunk with matching length and padding.");
                index++;
            }
            return dependencies.Values.ToArray();
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
        {
            throw new InvalidDataException("Malformed model source JSON.", error);
        }
    }

    internal static IReadOnlyList<ModelSourceDependency> DiscoverText(CapturedModelFile file, bool material, CancellationToken token)
    {
        string text;
        try { text = ModelSourcePath.Utf8.GetString(file.Bytes.Span); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("OBJ/MTL requires UTF-8 text.", error); }
        var body = text.AsSpan(); if (body.StartsWith("\uFEFF", StringComparison.Ordinal)) body = body[1..];
        var result = new Dictionary<string, ModelSourceDependency>(StringComparer.OrdinalIgnoreCase);
        foreach (var original in body.EnumerateLines())
        {
            token.ThrowIfCancellationRequested();
            if (ModelSourcePath.Utf8.GetByteCount(original) > 4096) throw new InvalidDataException("OBJ/MTL text line exceeds 4096 bytes.");
            var line = original; var comment = line.IndexOf('#'); if (comment >= 0) line = line[..comment]; line = line.Trim();
            if (line.IsEmpty) continue;
            if (line.EndsWith("\\", StringComparison.Ordinal)) throw new NotSupportedException("OBJ/MTL continuation lines are unsupported.");
            var end = 0; while (end < line.Length && !char.IsWhiteSpace(line[end])) end++;
            var keyword = line[..end].ToString();
            if (material)
            {
                if (keyword.StartsWith("map_", StringComparison.OrdinalIgnoreCase) || keyword.ToLowerInvariant() is "bump" or "disp" or "decal" or "refl" or "norm")
                    throw new NotSupportedException("MTL texture references are unsupported in capture profile v1.");
                continue;
            }
            if (keyword is "call" or "csh") throw new NotSupportedException("OBJ include/command directives are unsupported.");
            if (keyword != "mtllib")
            {
                if (keyword.Equals("mtllib", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("OBJ directives must use their lowercase spelling.");
                continue;
            }
            var names = line[end..].ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (names.Length == 0) throw new InvalidDataException("An OBJ mtllib directive requires a name.");
            foreach (var name in names)
            {
                var relative = ModelSourcePath.Resolve(file.Name, name);
                if (!Path.GetExtension(relative).Equals(".mtl", StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException("Only plain .mtl dependencies are supported for OBJ.");
                Add(result, new(relative, 0, Material: true), file.Name);
            }
        }
        return result.Values.ToArray();
    }

    private static void Add(Dictionary<string, ModelSourceDependency> dependencies, ModelSourceDependency dependency, string root)
    {
        if (dependency.Name.Equals(root, StringComparison.OrdinalIgnoreCase) && dependency.Name != root)
            throw new InvalidDataException("Case-insensitive model dependency name collision.");
        if (dependencies.TryGetValue(dependency.Name, out var existing))
        {
            if (existing.Name != dependency.Name) throw new InvalidDataException("Case-insensitive model dependency name collision.");
            dependencies[dependency.Name] = dependency with { MinimumBytes = Math.Max(existing.MinimumBytes, dependency.MinimumBytes), Material = existing.Material || dependency.Material };
            return;
        }
        if (dependency.Name != root && 1 + dependencies.Values.Count(value => value.Name != root) >= ModelSourceCaptureLimits.MaximumFiles)
            throw new InvalidDataException("Declared model dependency closure exceeds the file-count budget.");
        dependencies.Add(dependency.Name, dependency);
    }

    private static (ReadOnlyMemory<byte> Json, int? BinLength) Glb(ReadOnlyMemory<byte> bytes)
    {
        var span = bytes.Span;
        if (span.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(span) != 0x46546c67 ||
            BinaryPrimitives.ReadUInt32LittleEndian(span[4..]) != 2 || BinaryPrimitives.ReadUInt32LittleEndian(span[8..]) != span.Length)
            throw new InvalidDataException("Invalid GLB header, version or total length.");
        ReadOnlyMemory<byte> json = default; int? bin = null; var position = 12;
        while (position < span.Length)
        {
            if (span.Length - position < 8) throw new InvalidDataException("Truncated GLB chunk.");
            var size = BinaryPrimitives.ReadUInt32LittleEndian(span[position..]);
            var kind = BinaryPrimitives.ReadUInt32LittleEndian(span[(position + 4)..]); position += 8;
            if (size % 4 != 0 || size > span.Length - position) throw new InvalidDataException("Invalid GLB chunk length or alignment.");
            if (kind == 0x4e4f534a && position == 20) json = bytes.Slice(position, (int)size);
            else if (kind == 0x004e4942 && !json.IsEmpty && bin == null) bin = (int)size;
            else throw new NotSupportedException("Capture profile v1 permits one JSON chunk followed by at most one BIN chunk.");
            position += (int)size;
        }
        if (json.IsEmpty) throw new InvalidDataException("A GLB JSON chunk is required.");
        return (json, bin);
    }

    private static void ValidateJson(ReadOnlySpan<byte> bytes, CancellationToken token)
    {
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 64 });
        var objects = new Stack<HashSet<string>>(); var tokens = 0;
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            if (++tokens > 131072) throw new InvalidDataException("Model JSON exceeds the token budget.");
            if (reader.TokenType == JsonTokenType.StartObject) objects.Push(new(StringComparer.Ordinal));
            else if (reader.TokenType == JsonTokenType.EndObject) objects.Pop();
            else if (reader.TokenType == JsonTokenType.PropertyName && !objects.Peek().Add(reader.GetString()!))
                throw new InvalidDataException("Duplicate JSON property names are ambiguous.");
        }
    }
}
