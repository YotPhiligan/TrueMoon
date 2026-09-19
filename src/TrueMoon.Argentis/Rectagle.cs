namespace TrueMoon.Argentis;

/// <summary>A rectangle primitive. Retains the original prototype spelling for source compatibility.</summary>
public class Rectagle : Element
{
    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context) => context.Fill(Bounds, Foreground);
}
