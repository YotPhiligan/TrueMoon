using SkiaSharp;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Rendering.Skia;

/// <summary>Immutable font selection shared by Skia backends: explicit file snapshots or an opt-in system family chain.</summary>
/// <remarks>File catalogs never look up system fonts. System catalogs try the requested family, configured
/// fallback families, then a file catalog or the system default. This is family fallback, not per-glyph fallback.
/// Each surface owns its native fonts. The catalog holds only managed data and needs no disposal.</remarks>
public sealed class SkiaFontCatalog
{
    /// <summary>The default system policy: requested family, Segoe UI Variable Text, Segoe UI, then the system default.</summary>
    /// <remarks>Contains managed configuration only. Native font lookup occurs on each surface's cache misses,
    /// after the rendering backend initializes Skia. Explicitly passing this policy also preserves the
    /// parameterless rich-text glyph fallback. Explicit file catalogs retain file-only selection.</remarks>
    public static SkiaFontCatalog SystemDefault { get; } = new([FontDefaults.Family, FontDefaults.FallbackFamily], null);

    private readonly Dictionary<string, byte[]> _faces = new(StringComparer.Ordinal);
    private readonly byte[]? _defaultFace;
    private readonly Dictionary<(string Family, int Weight, bool Italic), byte[]> _variants = [];
    private readonly string[]? _systemFamilies;
    private readonly SkiaFontCatalog? _fallbackCatalog;
    private readonly string? _systemIdentity;

    private SkiaFontCatalog(Dictionary<string, byte[]> faces, string defaultFamily)
    { _faces = faces; _defaultFace = faces[defaultFamily]; }

    private SkiaFontCatalog(string[] families, SkiaFontCatalog? fallback)
    {
        _systemFamilies = families; _fallbackCatalog = fallback;
        _systemIdentity = string.Concat(families.Select(family => $"{family.Length}:{family}"));
    }

    /// <summary>Uses installed fonts with an ordered family fallback chain.</summary>
    /// <param name="fallbackFamilies">Families tried after the requested family; copied before returning. An empty list is allowed.</param>
    /// <param name="fallback">Optional explicit file catalog used when no system family matches; null uses the system default.</param>
    /// <returns>A managed, immutable selection policy; each surface owns its native fonts.</returns>
    /// <remarks>Use family names reported by Skia, for example Segoe UI Variable Text, with a separate weight600 for semibold.
    /// System font availability can vary between machines. Optical-size axes are not automatically changed with text size.</remarks>
    public static SkiaFontCatalog FromSystemFonts(IReadOnlyList<string> fallbackFamilies, SkiaFontCatalog? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(fallbackFamilies);
        if (fallback?._systemFamilies != null) throw new ArgumentException("The final fallback must be an explicit file catalog.", nameof(fallback));
        var families = fallbackFamilies.ToArray();
        foreach (var family in families) ArgumentException.ThrowIfNullOrWhiteSpace(family);
        SkiaNativeLibrary.Initialize();
        return new SkiaFontCatalog(families, fallback);
    }

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

    internal SKTypeface CreateTypeface(string family) => CreateTypeface(family, 400, false);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        if (weight is < 100 or > 900) throw new ArgumentOutOfRangeException(nameof(weight));
        if (_systemFamilies != null)
        {
            using var style = new SKFontStyle(weight, 5, italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
            var face = MatchSystemFamily(family, style);
            if (face != null) return face;
            foreach (var candidate in _systemFamilies)
            {
                face = MatchSystemFamily(candidate, style);
                if (face != null) return face;
            }
            return _fallbackCatalog?.CreateTypeface(family, weight, italic) ?? SKTypeface.FromFamilyName(null, style);
        }
        if (_variants.TryGetValue((family, weight, italic), out var bytes)) return Decode(bytes, family);
        return Decode(_faces.GetValueOrDefault(family, _defaultFace!), family);
    }

    private static SKTypeface? MatchSystemFamily(string family, SKFontStyle style)
    {
        var face = SKFontManager.Default.MatchFamily(family, style);
        // Some font managers return their default for an unknown name; do not stop the chain there.
        if (face == null || string.Equals(face.FamilyName, family, StringComparison.OrdinalIgnoreCase)) return face;
        face.Dispose();
        return null;
    }

    internal string RichFaceIdentity(string family, int weight, bool italic)
    {
        if (_systemFamilies != null)
            return $"system:{family.Length}:{family}:{weight}:{italic}:{_systemIdentity}:fallback:" +
                (_fallbackCatalog?.RichFaceIdentity(family, weight, italic) ?? "default");
        var bytes = _variants.GetValueOrDefault((family, weight, italic)) ?? _faces.GetValueOrDefault(family, _defaultFace!);
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
