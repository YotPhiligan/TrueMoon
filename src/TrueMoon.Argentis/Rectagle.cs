using TrueMoon.Argentis.Painting;

namespace TrueMoon.Argentis;

/// <summary>A rectangle primitive. Retains the original prototype spelling for source compatibility.</summary>
public class Rectagle : Element
{
    private static readonly ElementPresentation RectaglePresentation = ElementPresentation.Create<Rectagle>(static (rectangle, context) =>
        context.Fill(rectangle.Bounds, rectangle.Foreground));
    /// <inheritdoc />
    public override ElementPresentation Presentation => RectaglePresentation;
}
