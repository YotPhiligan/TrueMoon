using TrueMoon.Argentis.Painting;

namespace TrueMoon.Argentis;

/// <summary>Displays a borrowed decoded image; its caller owns the image lifetime.</summary>
public sealed class Image : Element
{
    private IImageSource? _source;
    /// <summary>The decoded image.</summary>
    public IImageSource? Source { get => _source; set { VerifyAccess(); _source = value; Invalidate(Invalidation.Layout); } }
    /// <inheritdoc />
    protected override Size MeasureCore(Size available, ITextLayoutService text) => Source?.Size ?? default;
}