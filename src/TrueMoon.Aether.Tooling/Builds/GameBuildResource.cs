using System.Text.Json;

namespace TrueMoon.Aether.Tooling.Builds;

internal readonly record struct GameBuildResource(string Path, string? LogicalName, string? WithCulture)
{
    internal static GameBuildResource Read(JsonElement item, string directory, bool prepared)
    {
        string? Metadata(string name) => item.TryGetProperty(name, out var value) ? value.GetString() : null;
        var path = item.GetProperty("FullPath").GetString()!;
        var logicalName = Metadata("LogicalName");
        var withCulture = Metadata("WithCulture");
        var type = Metadata("Type");
        ValidateField(path);
        if (logicalName is not null) ValidateField(logicalName);
        if (prepared && Metadata("ManifestResourceName") is { } manifestName) ValidateField(manifestName);
        if (System.IO.Path.GetExtension(path).ToLowerInvariant() is ".resx" or ".restext" or ".licx" or ".editorconfig" ||
            !string.IsNullOrEmpty(Metadata("Culture")) || Metadata("DependentUpon") != null ||
            Metadata("Link") != null || !string.IsNullOrEmpty(Metadata("OutputResource")) ||
            (prepared ? type != "Non-Resx" || !string.Equals(withCulture, "false", StringComparison.OrdinalIgnoreCase) :
                !string.IsNullOrEmpty(type) || !string.IsNullOrEmpty(Metadata("ManifestResourceName")) ||
                withCulture != null && !withCulture.Equals("false", StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException("Only non-localized original files can be captured as embedded resources: " + path);
        return new(System.IO.Path.GetFullPath(path, directory), logicalName, withCulture);
    }

    internal static (string File, string? Name, string? Visibility) ParseArgument(string value)
    {
        var payload = value.AsSpan();
        // The SDK can quote the entire argument when the path contains spaces.
        if (payload.Length >= 2 && payload[0] == '"' && payload[^1] == '"' && !payload[1..^1].Contains('"'))
            payload = payload[1..^1];

        var file = TakeField(ref payload, out var more);
        var name = more ? TakeField(ref payload, out more) : null;
        var visibility = more ? TakeField(ref payload, out more) : null;
        if (more || visibility != null && !visibility.Equals("public", StringComparison.OrdinalIgnoreCase) &&
            !visibility.Equals("private", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Unsupported captured /resource visibility or field count.");
        return (file, name, visibility);
    }

    private static string TakeField(ref ReadOnlySpan<char> payload, out bool more)
    {
        ReadOnlySpan<char> field;
        if (!payload.IsEmpty && payload[0] == '"')
        {
            var close = payload[1..].IndexOf('"') + 1;
            if (close == 0 || close + 1 < payload.Length && payload[close + 1] != ',')
                throw new NotSupportedException("Unbalanced or ambiguous captured /resource quoting.");
            field = payload[1..close];
            payload = payload[(close + 1)..];
        }
        else
        {
            var split = payload.IndexOf(',');
            if (split < 0) split = payload.Length;
            field = payload[..split];
            payload = payload[split..];
        }
        more = !payload.IsEmpty;
        if (more) payload = payload[1..];
        ValidateField(field);
        return field.ToString();
    }

    private static void ValidateField(ReadOnlySpan<char> field)
    {
        if (field.IsEmpty || field.Trim().Length != field.Length || field.IndexOfAny(",\"\r\n\0") >= 0)
            throw new NotSupportedException("Unsupported captured embedded resource punctuation or empty field.");
    }
}
