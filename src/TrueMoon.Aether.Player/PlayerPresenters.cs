using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Painting;

namespace TrueMoon.Aether.Player;

/// <summary>Player canvas and HUD rendering for windowed and directly hosted sessions.</summary>
public static class PlayerPresenters
{
    /// <summary>Registers the Player's letterboxed image and non-interactive HUD for this renderer.</summary>
    /// <param name="presenters">The registry being configured for the session or standalone draw.</param>
    public static void Register(UiPresenters presenters)
    {
        ArgumentNullException.ThrowIfNull(presenters);
        presenters.Register<PlayerCanvas>(Draw);
    }

    private static void Draw(PlayerCanvas canvas, IDrawingContext context)
    {
        var state = canvas.BeginDraw();
        if (state.Frame is not {} frame || state.Image is not {} image ||
            state.VisibleImage.Width <= 0 || state.VisibleImage.Height <= 0) return;
        context.Image(image, state.Bounds);
        if (state.Hud is {} hud) DrawHud(context, state, hud);
        canvas.CompleteDraw(state, frame);
    }

    private static void DrawHud(IDrawingContext context, in PlayerCanvas.DrawState state, GameHudSnapshot hud)
    {
        const float margin = 8;
        var viewport = state.Bounds;
        var line = state.FontSize + 6;
        var width = Math.Min(400, viewport.Width - 2 * margin);
        var height = viewport.Height - 2 * margin;
        if (width < 2 * margin || height < line + 2 * margin) return;

        var prompt = !string.IsNullOrWhiteSpace(hud.Prompt);
        var status = !string.IsNullOrWhiteSpace(hud.Status);
        var availableRows = Math.Min((int)((height - 2 * margin) / line), GameHudSnapshot.JournalCapacity + 2);
        var rows = Math.Max(0, availableRows - (prompt ? 1 : 0) - (status ? 1 : 0));
        var journalRows = Math.Min(rows, hud.Journal.Length);
        var lineCount = Math.Min(availableRows, (prompt ? 1 : 0) + (status ? 1 : 0) + journalRows);
        if (lineCount == 0) return;

        var card = new Rect(viewport.X + margin, viewport.Y + margin, width, lineCount * line + 2 * margin);
        context.Fill(card, state.Card, state.Radius);
        context.Save();
        try
        {
            context.Clip(card.Deflate(new Thickness(margin)));
            var x = card.X + margin;
            var y = card.Y + margin;
            if (prompt) Show(context, state, hud.Prompt!, x, ref y, line);
            if (status && (!prompt || availableRows >= 2)) Show(context, state, hud.Status!, x, ref y, line);
            for (var index = hud.Journal.Length - journalRows; index < hud.Journal.Length; index++)
                Show(context, state, hud.Journal[index], x, ref y, line);
        }
        finally { context.Restore(); }
    }

    private static void Show(IDrawingContext context, in PlayerCanvas.DrawState state, string text, float x, ref float y, float line)
    {
        context.Text(text, x, y, state.Foreground, state.FontSize, state.Family, state.Weight, state.Italic);
        y += line;
    }
}
