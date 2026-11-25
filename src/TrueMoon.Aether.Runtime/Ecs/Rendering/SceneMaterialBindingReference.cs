using System.Security.Cryptography;
using System.Text;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Runtime.Ecs.Rendering;

// Combined overrides are scene data. A build publishes the computed binding ID and the exact ordered slot keys.
internal static class SceneMaterialBindingReference
{
    private const string Prefix = "slots/1:";

    internal static string Format(IEnumerable<(Guid SlotId, Guid MaterialId)> overrides) =>
        Prefix + string.Join(';', overrides.OrderBy(item => item.SlotId).Select(item => $"{item.SlotId:N}={item.MaterialId:N}"));

    internal static bool TryParse(Guid mesh, string value, out Guid binding, out (Guid SlotId, Guid MaterialId)[] overrides)
    {
        binding = default;
        overrides = [];
        if (value.Length == 36 && Guid.TryParseExact(value, "D", out binding))
            return binding != Guid.Empty;
        if (!value.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        const int pairLength = 65;
        var fields = value[Prefix.Length..].Split(';');
        if (fields.Length is < 2 or > RenderContentProfile.MaximumSlots) return false;
        var parsed = new (Guid SlotId, Guid MaterialId)[fields.Length];
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index].AsSpan();
            if (field.Length != pairLength || field[32] != '=' ||
                !Guid.TryParseExact(field[..32], "N", out var slot) || slot == Guid.Empty ||
                !Guid.TryParseExact(field[33..], "N", out var material) || material == Guid.Empty ||
                index > 0 && parsed[index - 1].SlotId.CompareTo(slot) >= 0)
                return false;
            parsed[index] = (slot, material);
        }
        if (!string.Equals(value, Format(parsed), StringComparison.Ordinal)) return false;
        var bytes = Encoding.ASCII.GetBytes($"Aether.Scene.MaterialBinding/3:{mesh:N}:{value}");
        binding = new Guid(SHA256.HashData(bytes).AsSpan(0, 16));
        if (binding == Guid.Empty) return false;
        overrides = parsed;
        return true;
    }
}
