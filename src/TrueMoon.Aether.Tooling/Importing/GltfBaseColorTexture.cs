using System.Text.Json;

namespace TrueMoon.Aether.Tooling.Importing;

internal sealed record GltfBaseColorTexture(string Name, int MaterialIndex)
{
    internal static GltfBaseColorTexture? Read(JsonElement model, string root)
    {
        var images = Array("images");
        var textures = Array("textures");
        var samplers = Array("samplers");
        if (Count(images) == 0 && Count(textures) == 0 && Count(samplers) == 0)
            return null;
        var materialIndex = -1;
        if (model.TryGetProperty("materials", out var materials))
        {
            var index = 0;
            foreach (var material in materials.EnumerateArray())
            {
                if (material.TryGetProperty("pbrMetallicRoughness", out var pbr) &&
                    pbr.TryGetProperty("baseColorTexture", out var reference))
                {
                    if (materialIndex >= 0 || reference.GetProperty("index").GetInt32() != 0 ||
                        reference.TryGetProperty("texCoord", out var uv) && uv.GetInt32() != 0)
                        throw new NotSupportedException("The PNG profile supports one base-color reference using TEXCOORD_0.");
                    materialIndex = index;
                }
                index++;
            }
        }
        if (!Path.GetExtension(root).Equals(".gltf", StringComparison.OrdinalIgnoreCase) ||
            Count(images) != 1 || Count(textures) != 1 || Count(samplers) != 1 || materialIndex < 0)
            throw new NotSupportedException("One external PNG with one explicit sampler and a base-color reference is required.");

        var image = images[0];
        if (image.TryGetProperty("bufferView", out _) || !image.TryGetProperty("uri", out var uri) ||
            image.TryGetProperty("mimeType", out var mime) && mime.GetString() != "image/png")
            throw new NotSupportedException("Only an external PNG is supported for the base-color image.");
        var name = ModelSourcePath.Resolve(root, uri.GetString()!);
        if (!Path.GetExtension(name).Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("The base-color image must be a PNG.");

        var texture = textures[0];
        if (!ValueIs(texture, "source", 0) || !ValueIs(texture, "sampler", 0))
            throw new NotSupportedException("The base-color texture must reference the PNG and explicit sampler.");
        var sampler = samplers[0];
        if (!ValueIs(sampler, "magFilter", 9728) || !ValueIs(sampler, "minFilter", 9728) ||
            !ValueIs(sampler, "wrapS", 33071) || !ValueIs(sampler, "wrapT", 33071))
            throw new NotSupportedException("The base-color sampler must use nearest filtering and clamp-to-edge on both axes.");
        return new(name, materialIndex);

        JsonElement Array(string key) => model.TryGetProperty(key, out var value) ? value : default;
        static int Count(JsonElement value) => value.ValueKind == JsonValueKind.Undefined ? 0 : value.GetArrayLength();
        static bool ValueIs(JsonElement obj, string key, int expected) => obj.TryGetProperty(key, out var value) && value.GetInt32() == expected;
    }
}
