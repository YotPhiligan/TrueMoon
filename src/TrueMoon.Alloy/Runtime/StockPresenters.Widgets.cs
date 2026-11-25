using TrueMoon.Argentis;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;

namespace TrueMoon.Alloy.Runtime;

internal static partial class StockPresenters
{
    internal static IFactory<IElementPresenter<IconButtonPaintState>> StockIconFactory { get; } =
        new Factory<IElementPresenter<IconButtonPaintState>>(static () => new IconButtonPresenter());

    internal static void AddWidgets(Dictionary<Type, ElementPresentation> entries)
    {
        entries.Add(typeof(Text), ElementPresentation.Create<Text>(new Factory<IElementPresenter<Text>>(static () => new TextPresenter())));
        entries.Add(typeof(Button), ElementPresentation.Create<Button>(new Factory<IElementPresenter<Button>>(static () => new ButtonPresenter())));
        entries.Add(typeof(CheckBox), ElementPresentation.Create<CheckBox>(DrawCheckBox));
        entries.Add(typeof(IconButton), ElementPresentation.Create<IconButton, IconButtonPaintState>(IconButtonPaintState.From, StockIconFactory));
        entries.Add(typeof(Image), ElementPresentation.Create<Image>(static (image, context) =>
        {
            if (image.Source is { } source) context.Image(source, image.Bounds.Deflate(image.Padding));
        }));
        entries.Add(typeof(ProgressBar), ElementPresentation.Create<ProgressBar>(ProgressBarPresenter.DrawFill));
        entries.Add(typeof(Rectangle), ElementPresentation.Create<Rectangle>(static (rectangle, context) =>
            context.Fill(rectangle.Bounds, rectangle.Foreground)));
        entries.Add(typeof(Slider), ElementPresentation.Create<Slider>(DrawSlider));
        entries.Add(typeof(StatusBar), ElementPresentation.Create<StatusBar>(DrawStatusBar));
    }

    private static void DrawCheckBox(CheckBox checkBox, IDrawingContext context)
    {
        var bounds = checkBox.Bounds;
        var theme = checkBox.Theme;
        var area = bounds.Deflate(checkBox.Padding);
        context.Text(checkBox.Value, area.X, area.Y + Math.Max(0, (area.Height - checkBox.LabelSize.Height) / 2),
            checkBox.IsEffectivelyEnabled ? checkBox.Foreground : theme.Disabled, checkBox.FontSize, checkBox.FontFamily, checkBox.FontWeight, checkBox.FontItalic);
        var box = new Rect(bounds.X + 10, bounds.Y + (bounds.Height - 16) / 2, 16, 16);
        var radius = Math.Min(checkBox.CornerRadius, box.Width / 2);
        var color = !checkBox.IsEffectivelyEnabled ? theme.Disabled : checkBox.IsReadOnly ? theme.Border :
            checkBox.ColorRole == EditorColorRole.Neutral ? theme.Accent : theme.EditorColor(checkBox.ColorRole);
        if (checkBox.IsChecked) { context.Fill(box, color, radius); UiSymbols.Draw(context, UiSymbol.Check, box.Deflate(new Thickness(3)),
            checkBox.ColorRole == EditorColorRole.Neutral ? theme.OnAccent : theme.OnEditorAccent); }
        if (!checkBox.IsChecked || checkBox.ColorRole != EditorColorRole.Neutral || checkBox.HasError || checkBox.IsModified || checkBox.IsFocused || checkBox.IsHovered || checkBox.IsReadOnly)
            context.Stroke(box.Deflate(new Thickness(.5f)), checkBox.BorderColor, 1, Math.Max(0, radius - .5f));
    }

    private static void DrawSlider(Slider slider, IDrawingContext context)
    {
        var bounds = slider.Bounds;
        var theme = slider.Theme;
        var track = new Rect(bounds.X + 8, bounds.Y + (bounds.Height - 4) / 2, Math.Max(0, bounds.Width - 16), 4);
        var color = slider.IsEffectivelyEnabled ? theme.Accent : theme.Disabled;
        context.Fill(track, theme.Border);
        context.Fill(track with { Width = track.Width * slider.Value }, color);
        var thumb = new Rect(track.X + track.Width * slider.Value - 7, bounds.Y + (bounds.Height - 16) / 2, 14, 16);
        context.Fill(thumb, slider.IsDragging ? theme.ControlPressed : slider.IsHovered ? theme.ControlHover : theme.Control);
        context.Stroke(thumb.Deflate(new Thickness(.5f)), color, 1);
    }

    private static void DrawStatusBar(StatusBar bar, IDrawingContext context)
    {
        var bounds = bar.Bounds;
        context.Fill(new(bounds.X, bounds.Y, bounds.Width, Math.Min(1, bounds.Height)),
            bar.OverlayContent ? bar.Theme.Border with { A = (byte)MathF.Round(40 + 100 * bar.OverlayReveal) } : bar.Theme.Border);
    }
}
