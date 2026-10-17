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
    private readonly Dictionary<(string Family, int Weight, bool Italic), byte[]> _variants = [];

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

    /// <summary>Loads explicit normal/bold/italic faces without changing the process font mapper.</summary>
    /// <param name="defaultFamily">Registered family with a normal weight400 face.</param>
    /// <param name="faces">Family/weight/slant file descriptors. Each family needs a normal weight400 face.</param>
    /// <returns>Immutable snapshots, including style variants used by rich text.</returns>
    public static SkiaFontCatalog LoadFaces(string defaultFamily, IReadOnlyList<SkiaFontFace> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        var regular = new Dictionary<string, string>(StringComparer.Ordinal);
        var keys = new HashSet<(string, int, bool)>();
        foreach (var face in faces)
        {
            ArgumentNullException.ThrowIfNull(face);
            ArgumentException.ThrowIfNullOrWhiteSpace(face.Family);
            ArgumentException.ThrowIfNullOrWhiteSpace(face.Path);
            if (face.Weight is < 100 or > 900) throw new ArgumentOutOfRangeException(nameof(faces));
            if (!keys.Add((face.Family, face.Weight, face.Italic))) throw new ArgumentException("Duplicate font face.", nameof(faces));
            if (face.Weight == 400 && !face.Italic) regular.Add(face.Family, face.Path);
        }
        if (faces.Any(face => !regular.ContainsKey(face.Family))) throw new ArgumentException("Each family requires its normal weight400 face.", nameof(faces));
        var catalog = Load(defaultFamily, regular);
        foreach (var face in faces)
        {
            var bytes = File.ReadAllBytes(face.Path);
            using var decoded = Decode(bytes, face.Path);
            catalog._variants.Add((face.Family, face.Weight, face.Italic), bytes);
        }
        return catalog;
    }

    internal SKTypeface CreateTypeface(string family, int weight, bool italic)
    {
        if (_variants.TryGetValue((family, weight, italic), out var bytes)) return Decode(bytes, family);
        return CreateTypeface(family);
    }

    internal string RichFaceIdentity(string family, int weight, bool italic)
    {
        var bytes = _variants.GetValueOrDefault((family, weight, italic)) ?? _faces.GetValueOrDefault(family, _defaultFace);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    private static SKTypeface Decode(byte[] bytes, string name)
    {
        using var data = SKData.CreateCopy(bytes);
        return SKTypeface.FromData(data) ?? throw new ArgumentException($"Could not decode font '{name}'.");
    }
}

/// <summary>An explicit font-family style variant supplied by the application.</summary>
/// <param name="Family">Ordinal family alias.</param><param name="Path">Licensed font file.</param>
/// <param name="Weight">Font weight100..900;400 is normal and700 bold.</param><param name="Italic">Italic face.</param>
public sealed record SkiaFontFace(string Family, string Path, int Weight = 400, bool Italic = false);
