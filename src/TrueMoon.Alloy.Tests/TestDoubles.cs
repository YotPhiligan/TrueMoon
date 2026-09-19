using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Tests;

internal sealed class FixedTextLayout : ITextLayoutService
{
    public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize);
}

internal class SizedElement(float width = 30, float height = 20) : Element
{
    public Size Available { get; private set; }
    public Rect ContentBounds { get; private set; }
    protected override Size MeasureCore(Size available, ITextLayoutService text)
    {
        Available = available;
        return new Size(width, height);
    }
    protected override void ArrangeCore(Rect contentBounds) => ContentBounds = contentBounds;
}

internal sealed class RecordingDrawingContext : IDrawingContext
{
    public int Saves { get; private set; }
    public int Restores { get; private set; }
    public List<Rect> Clips { get; } = [];
    public bool ThrowOnFill { get; set; }
    public void Save() => Saves++;
    public void Restore() => Restores++;
    public void Clip(Rect rectangle) => Clips.Add(rectangle);
    public void Translate(float x, float y) { }
    public void Fill(Rect rectangle, Color color, float radius = 0)
    {
        if (ThrowOnFill) throw new InvalidOperationException("Drawing failed.");
    }
    public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
    public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) { }
    public void Image(IImageSource source, Rect destination) { }
}

internal sealed class DisposalCounter : IDisposable
{
    public int Count { get; private set; }
    public void Dispose() => Count++;
}
