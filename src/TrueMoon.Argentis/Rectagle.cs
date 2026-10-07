namespace TrueMoon.Argentis;

/// <summary>A rectangle primitive. Retains the original prototype spelling for source compatibility.</summary>
public class Rectagle : Element
{
    /// <summary>Creates a new Rectagle with its constructor defaults.</summary>
    /// <returns>A new independent Rectagle for Fluent configuration.</returns>
    public static Rectagle Create() => new();

    /// <inheritdoc />
    protected override void DrawCore(IDrawingContext context) => context.Fill(Bounds, Foreground);
}
