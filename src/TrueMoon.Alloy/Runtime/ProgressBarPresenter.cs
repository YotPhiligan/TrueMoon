using TrueMoon.Argentis;
using TrueMoon.Argentis.Painting;

namespace TrueMoon.Alloy.Runtime;

/// <summary>Stock drawing operations shared by progress indicators.</summary>
public static class ProgressBarPresenter
{
    /// <summary>Draws the normalized fill within the progress bar border.</summary>
    public static void DrawFill(ProgressBar progress, IDrawingContext context)
    {
        var area = progress.Bounds.Deflate(new Thickness(progress.BorderWidth));
        context.Fill(area with { Width = area.Width * progress.Value },
            progress.IsEffectivelyEnabled ? progress.Theme.Accent : progress.Theme.Disabled, progress.CornerRadius);
    }
}
