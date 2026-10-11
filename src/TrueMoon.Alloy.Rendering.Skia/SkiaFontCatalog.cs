using SkiaSharp;

namespace TrueMoon.Alloy.Rendering.Skia;

/// <summary>Immutable font-file snapshots shared by Skia backends without system font lookup.</summary>
/// <remarks>Unregistered family names use the explicit default face. Missing glyphs use that face's
/// missing-glyph symbol; this catalog does not introduce shaping or system/emoji fallback.
/// Each surface owns its native fonts. The catalog holds only managed bytes and needs no disposal.</remarks>
public sealed class SkiaFontCatalog
{
    private readonly Dictionary<string, byte[]> _faces;
    private readonly byte[] _defaultFace;

    private SkiaFontCatalog(Dictionary<string, byte[]> faces, string defaultFamily)
    { _faces = faces; _defaultFace = faces[defaultFamily]; }

    /// <summary>Reads and validates font files once, retaining independent immutable snapshots.</summary>
    /// <param name="defaultFamily">Registered alias used for every unregistered family.</param>
    /// <param name="files">Ordinal, case-sensitive family aliases mapped to font-file paths.</param>
    /// <returns>A thread-safe catalog whose lifetime is independent of the source files.</returns>
    /// <exception cref="ArgumentException">An alias is invalid, the default is absent or a file is not a font.</exception>
    public static SkiaFontCatalog Load(string defaultFamily, IReadOnlyDictionary<string, string> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultFamily);
        ArgumentNullException.ThrowIfNull(files);
        if (!files.ContainsKey(defaultFamily)) throw new ArgumentException("Register the default font family.", nameof(defaultFamily));
        SkiaNativeLibrary.Initialize();
        var faces = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var paths = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (family, path) in files)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(family);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var fullPath = Path.GetFullPath(path);
            if (!paths.TryGetValue(fullPath, out var bytes))
            {
                bytes = File.ReadAllBytes(fullPath);
                using var face = Decode(bytes, path);
                paths.Add(fullPath, bytes);
            }
            faces.Add(family, bytes);
        }
        if (!faces.ContainsKey(defaultFamily)) throw new ArgumentException("Register the exact default font alias.", nameof(defaultFamily));
        return new SkiaFontCatalog(faces, defaultFamily);
    }

    internal SKTypeface CreateTypeface(string family) => Decode(_faces.GetValueOrDefault(family, _defaultFace), family);

    private static SKTypeface Decode(byte[] bytes, string name)
    {
        using var data = SKData.CreateCopy(bytes);
        return SKTypeface.FromData(data) ?? throw new ArgumentException($"Could not decode font '{name}'.");
    }
}
