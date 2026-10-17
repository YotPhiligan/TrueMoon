using System.Security.Cryptography;
using TrueMoon.Alloy.Rendering.Skia;

namespace AlloyPackageConsumer;

internal static class ConsumerFonts
{
    internal static readonly string[] Files = ["NotoSans-Regular.ttf", "NotoSansSymbols-Regular.ttf", "OFL.txt"];

    internal static Dictionary<string, string> Fingerprints() => Files.ToDictionary(name => name,
        name => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", name)))));

    internal static SkiaFontCatalog Load() => SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
    {
        ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", Files[0]),
        ["Segoe UI Symbol"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", Files[1])
    });
}
