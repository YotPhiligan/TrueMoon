namespace TrueMoon.Argentis;

// Keep these contours and colors in sync with the standalone Argentis SVG.
internal static class TrueMoonMark
{
    private static readonly VectorIcon Rim = new("M 12 1.5 A 10.5 10.5 0 1 1 12 22.5 A 10.5 10.5 0 1 1 12 1.5 Z");
    private static readonly VectorIcon Disc = new("M 12 2.8 A 9.2 9.2 0 1 1 12 21.2 A 9.2 9.2 0 1 1 12 2.8 Z");
    private static readonly VectorIcon Nacre = new("M 20.2 7.9 A 9.2 9.2 0 0 1 6.5 19.4 C 11.8 20.8 17.1 18 19 13.8 C 19.9 11.9 20.2 9.7 20.2 7.9 Z");
    private static readonly VectorIcon Mare = new("M 5.8 7.4 C 7.2 6.2 9.4 6.6 10.2 8 C 11 9.4 12.1 9.7 13.7 9.5 C 15.3 9.4 16.3 10.6 16 12 C 15.8 13 14.8 13.7 13.5 13.8 C 11.6 14.1 11.5 15.3 10.2 16.2 C 8.3 17.5 5.6 16.3 5 14.3 C 4.6 12.9 5.2 12.1 5.1 10.9 C 4.9 9.7 4.7 8.5 5.8 7.4 Z");
    private static readonly VectorIcon Rune = new("M 8.8 9.6 V 14.3 M 7.2 11.3 L 8.8 9.6 L 10.9 11.3 M 8.8 13.2 L 11.4 12.2", 1.1f);

    internal static void Draw(IVectorIconDrawingContext context, Rect bounds, byte alpha)
    {
        context.DrawVectorIcon(Rim, bounds, new(113, 102, 124, alpha));
        context.DrawVectorIcon(Disc, bounds, new(244, 240, 245, alpha));
        context.DrawVectorIcon(Nacre, bounds, new(228, 204, 221, alpha));
        context.DrawVectorIcon(Mare, bounds, new(179, 168, 186, alpha));
        context.DrawVectorIcon(Rune, bounds, new(111, 96, 126, alpha));
    }
}
