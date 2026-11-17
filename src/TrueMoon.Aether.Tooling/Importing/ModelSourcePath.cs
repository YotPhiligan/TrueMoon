using System.Text;

namespace TrueMoon.Aether.Tooling.Importing;

internal static class ModelSourcePath
{
    internal static readonly UTF8Encoding Utf8 = new(false, true);

    internal static string Normalize(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 1024) throw new InvalidDataException("A raw model name exceeds the 1024-character spelling budget.");
        if (name[0] is '/' or '\\' || name.Any(c => char.IsControl(c) || "<>:\"|?*%#".Contains(c)))
            throw new NotSupportedException("Only plain relative model dependency names are supported.");
        var parts = name.Replace('\\', '/').Split('/');
        var normalized = new List<string>();
        foreach (var part in parts)
        {
            if (part == ".") continue;
            if (part.Length == 0 || part == ".." || part.EndsWith('.') || part.EndsWith(' '))
                throw new NotSupportedException("Empty, parent and Windows-ambiguous path segments are unsupported.");
            var stem = part.Split('.')[0];
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) || stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) ||
                stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && (stem[3] is >= '1' and <= '9' or '\u00b9' or '\u00b2' or '\u00b3'))
                throw new NotSupportedException("Windows device names are unsupported.");
            normalized.Add(part);
        }
        var result = string.Join('/', normalized);
        if (result.Length == 0 || Utf8.GetByteCount(result) > 256) throw new InvalidDataException("A model name exceeds the 256-byte path budget.");
        return result;
    }

    internal static string Resolve(string referringName, string dependency)
    {
        var relative = Normalize(dependency);
        var slash = referringName.LastIndexOf('/');
        return Normalize(slash < 0 ? relative : referringName[..(slash + 1)] + relative);
    }
}
