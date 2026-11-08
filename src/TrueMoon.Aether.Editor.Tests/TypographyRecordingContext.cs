using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Tests;

// Deliberately rejects legacy overloads: a dropped style on any custom text path must fail.
internal sealed class TypographyRecordingContext : ITextLayoutService, IDrawingContext
{
    internal sealed record TextCall(string Text, float X, float Y, float Size, string Family, int Weight, bool Italic);
    internal List<TextCall> Texts { get; } = [];
    internal List<TextCall> Measures { get; } = [];
    internal List<(Rect Bounds, Color Color)> Fills { get; } = [];
    public Size Measure(string text, float fontSize, string fontFamily) => throw new InvalidOperationException("Typography was omitted from measurement.");
    public Size Measure(string text, float fontSize, string fontFamily, int fontWeight, bool fontItalic)
    {
        Measures.Add(new(text, 0, 0, fontSize, fontFamily, fontWeight, fontItalic));
        return new(text.Length * (fontSize / 2 + (fontWeight - 400) / 100f + (fontItalic ? 1 : 0)), fontSize + 4);
    }
    public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) => throw new InvalidOperationException("Typography was omitted from drawing.");
    public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily, int fontWeight, bool fontItalic) =>
        Texts.Add(new(text, x, y, fontSize, fontFamily, fontWeight, fontItalic));
    public void Save() { }
    public void Restore() { }
    public void Clip(Rect rectangle) { }
    public void Translate(float x, float y) { }
    public void Fill(Rect rectangle, Color color, float radius = 0) => Fills.Add((rectangle, color));
    public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
    public void Image(IImageSource source, Rect destination) { }
}
