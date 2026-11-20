using System.Buffers.Binary;
using System.Text.Json;
using TrueMoon.Aether.Tooling.Importing;

namespace TrueMoon.Aether.Importing.Assimp;

// Explicit projection policy: retain UV0 for textured primitives, and omit normal and tangent.
internal static class GltfProfile
{
    internal static GltfBaseColorTexture? Validate(CapturedModelSource source)
    {
        var root = source.GetFile(source.RootName);
        foreach (var dependency in ModelSourceDependencies.Discover(root, CancellationToken.None))
            ModelImportProtocol.Require(source.GetFile(dependency.Name).Length >= dependency.MinimumBytes, "Truncated captured dependency.");
        var bytes = root.Bytes;
        if (Path.GetExtension(source.RootName).Equals(".glb", StringComparison.OrdinalIgnoreCase))
            bytes = bytes.Slice(20, checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Span[12..])));
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 }); var json = document.RootElement;
        CheckExtensions(json);
        var texture = GltfBaseColorTexture.Read(json, source.RootName);
        foreach (var feature in new[] { "animations", "skins", "cameras" })
            if (json.TryGetProperty(feature, out var value) && value.GetArrayLength() != 0) throw new NotSupportedException("Unsupported static model feature.");
        if (json.TryGetProperty("nodes", out var nodes)) foreach (var node in nodes.EnumerateArray())
            foreach (var feature in new[] { "skin", "camera", "weights" })
                if (node.TryGetProperty(feature, out _)) throw new NotSupportedException("Unsupported node feature.");
        foreach (var mesh in json.GetProperty("meshes").EnumerateArray())
        {
            if (mesh.TryGetProperty("weights", out _)) throw new NotSupportedException("Morph weights are unsupported.");
            foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
            {
                if (primitive.TryGetProperty("targets", out _) || primitive.TryGetProperty("mode", out var mode) && mode.GetInt32() != 4)
                    throw new NotSupportedException("Only static triangle primitives are supported.");
                var attributes = primitive.GetProperty("attributes"); attributes.GetProperty("POSITION").GetInt32();
                if (texture != null && primitive.TryGetProperty("material", out var selected) && selected.GetInt32() == texture.MaterialIndex)
                    ModelImportProtocol.Require(attributes.TryGetProperty("TEXCOORD_0", out _), "Textured primitives require TEXCOORD_0.");
                foreach (var attribute in attributes.EnumerateObject())
                    if (attribute.Name is not ("POSITION" or "NORMAL" or "TANGENT" or "TEXCOORD_0")) throw new NotSupportedException("Unsupported vertex attribute.");
            }
        }
        if (json.TryGetProperty("materials", out var materials)) foreach (var material in materials.EnumerateArray())
        {
            if (material.TryGetProperty("alphaMode", out var mode) && mode.GetString() != "OPAQUE" ||
                material.TryGetProperty("doubleSided", out var sided) && sided.GetBoolean()) throw new NotSupportedException("Only opaque single-sided materials are supported.");
            if (material.TryGetProperty("emissiveFactor", out var emissive) && emissive.EnumerateArray().Any(value => value.GetSingle() != 0))
                throw new NotSupportedException("Emissive materials are unsupported.");
            if (material.TryGetProperty("pbrMetallicRoughness", out var pbr))
            {
                foreach (var property in pbr.EnumerateObject())
                    if (property.Name.EndsWith("Texture", StringComparison.Ordinal) &&
                        (property.Name != "baseColorTexture" || texture == null))
                        throw new NotSupportedException("Only base-color textures are supported.");
                if (pbr.TryGetProperty("baseColorFactor", out var color))
                {
                    ModelImportProtocol.Require(color.GetArrayLength() == 4, "Base color length.");
                    foreach (var value in color.EnumerateArray()) Unit(value.GetSingle());
                    if (color[3].GetSingle() != 1) throw new NotSupportedException("Opaque profile requires alpha 1.");
                }
                // Metallic/roughness are validated but deliberately not represented by the opaque-unlit recipe.
                foreach (var name in new[] { "metallicFactor", "roughnessFactor" }) if (pbr.TryGetProperty(name, out var value)) Unit(value.GetSingle());
            }
            foreach (var name in new[] { "normalTexture", "occlusionTexture", "emissiveTexture" })
                if (material.TryGetProperty(name, out _)) throw new NotSupportedException("Material textures are unsupported.");
        }
        return texture;
    }
    private static void Unit(float value) => ModelImportProtocol.Require(float.IsFinite(value) && value is >= 0 and <= 1, "Material unit range.");
    private static void CheckExtensions(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
            {
                if (property.Name == "extensions" && property.Value.EnumerateObject().Any()) throw new NotSupportedException("glTF extensions are unsupported.");
                if (property.Name != "extras") CheckExtensions(property.Value);
            }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var element in value.EnumerateArray()) CheckExtensions(element);
    }
}
