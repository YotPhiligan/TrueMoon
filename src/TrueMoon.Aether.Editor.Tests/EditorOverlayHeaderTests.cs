using System.Globalization;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorOverlayHeaderTests
{
    [Fact]
    public void Caption_ShowsSceneAndDirtyMarkerWhileRetainingTheSameControlAndBaseProjectTitle()
    {
        using var test = new HeaderFixture(openDocument: true); test.Frame.TitleBarOverContent = true;
        using var ui = test.Ui(); ui.Update();
        var caption = test.Header.Caption; var baseTitle = "TrueMoon Aether Editor · room Мир 🛰️";
        Assert.Equal(baseTitle + " · room.aetherscene", caption.Value);
        Assert.Contains(test.Owner.Files.ScenePath, caption.AccessibleName);
        test.Transform.Fields[0].Value = "42"; test.Transform.Apply(); test.Documents.Refresh(); ui.Update();
        Assert.Same(caption, test.Header.Caption); Assert.Equal(baseTitle + " · room.aetherscene *", caption.Value);
        test.Documents.Request(DocumentCommand.Save); test.Owner.Complete(); test.Documents.Refresh(); ui.Update();
        Assert.Equal(baseTitle + " · room.aetherscene", caption.Value); Assert.DoesNotContain("*", caption.Value);
        ui.SetTheme(Theme.Light); ui.Update(); Assert.Same(caption, test.Header.Caption);
        test.Documents.Request(DocumentCommand.Close); test.Owner.Complete(); test.Documents.Refresh(); ui.Update();
        Assert.Equal(baseTitle, caption.Value); Assert.Same(caption, test.Header.Caption);
        Assert.Contains(test.Owner.Files.Project.ScenesDirectory, caption.AccessibleName);
    }

    [Fact]
    public void CaptionHover_ShowsFullPathAfterDelayBelowHeaderWithoutChangingContentOrStealingFocus()
    {
        using var test = new HeaderFixture(openDocument: true); test.Frame.TitleBarOverContent = true;
        test.Overlays.PopupInsets = new(0, 36, 0, 0);
        using var ui = test.Ui(); ui.Update(); ui.Focus(test.Header.Menu.MoreButton);
        var body = test.Frame.Content.Bounds; var caption = test.Header.Caption.Bounds;
        ui.HandleInput(new(InputKind.PointerMove, caption.X + caption.Width / 2, caption.Y + caption.Height / 2));
        test.Clock.Advance(499); ui.Update(); Assert.Null(test.Overlays.Popup);
        test.Clock.Advance(1); ui.Update();
        var popup = Assert.IsAssignableFrom<Element>(test.Overlays.Popup);
        Assert.Same(test.Header, test.Overlays.PopupOwner); Assert.True(test.Header.Menu.MoreButton.IsFocused);
        Assert.Contains(test.Owner.Files.ScenePath, string.Join("\n", DocumentUiFixture.Tree(popup).OfType<Text>().Select(text => text.Value)));
        Assert.True(popup.Bounds.Y >= test.Frame.DefaultTitleBar!.Bounds.Y + test.Frame.DefaultTitleBar.Bounds.Height);
        Assert.Equal(body, test.Frame.Content.Bounds); Assert.Equal(caption, test.Header.Caption.Bounds);
        Assert.True(((IWindowHeaderActivity)test.Header).KeepsTitleBarActive);
        ui.HandleInput(new(InputKind.PointerMove, body.X + body.Width / 2, 400)); test.Clock.Advance(160); ui.Update();
        Assert.Null(test.Overlays.Popup); Assert.Equal(body, test.Frame.Content.Bounds);
    }

    [Fact]
    public void CaptionTooltip_WrapsLongPathIntoSeparateGraphemeSafeLinesWithoutClippingOrMovingFocus()
    {
        using var test = new HeaderFixture(openDocument: true); test.Frame.TitleBarOverContent = true;
        test.Overlays.PopupInsets = new(0, 36, 0, 0);
        using var ui = test.Ui(); ui.Update(); ui.Focus(test.Header.Menu.MoreButton);
        var fullText = test.Header.Caption.Value + "\r\nC:\\Scenes\\" +
            string.Concat(Enumerable.Repeat("Мир/🛰️/e\u0301/👩‍💻/", 20)) + "scene.aetherscene";
        test.Header.Caption.AccessibleName = fullText; ui.Update();
        var body = test.Frame.Content.Bounds; var caption = test.Header.Caption.Bounds;
        ui.HandleInput(new(InputKind.PointerMove, caption.X + caption.Width / 2, caption.Y + caption.Height / 2));
        test.Clock.Advance(500); ui.Update();
        var popup = Assert.IsAssignableFrom<Element>(test.Overlays.Popup);
        var hint = Assert.Single(DocumentUiFixture.Tree(popup).OfType<Text>());
        Assert.Equal(fullText, hint.Value); Assert.InRange(popup.Bounds.Width, 1f, 574f);
        Assert.True(popup.Bounds.Height > hint.FontSize + 14);
        var drawing = new HintDrawing(); popup.Draw(drawing);
        Assert.True(drawing.Lines.Count > 2);
        var expected = fullText.Replace("\r", "").Replace("\n", "");
        Assert.Equal(expected, string.Concat(drawing.Lines.Select(line => line.Text)));
        var boundaries = StringInfo.ParseCombiningCharacters(expected).Append(expected.Length).ToHashSet();
        var end = 0; var lastY = float.NegativeInfinity;
        foreach (var line in drawing.Lines)
        {
            Assert.DoesNotContain("\r", line.Text); Assert.DoesNotContain("\n", line.Text);
            Assert.True(line.Y > lastY); lastY = line.Y;
            end += line.Text.Length; Assert.Contains(end, boundaries);
        }
        Assert.Contains(drawing.Lines, line => line.Text.Contains("🛰️", StringComparison.Ordinal));
        Assert.Contains(drawing.Lines, line => line.Text.Contains("👩‍💻", StringComparison.Ordinal));
        Assert.True(test.Header.Menu.MoreButton.IsFocused);
        Assert.Equal(body, test.Frame.Content.Bounds); Assert.Equal(caption, test.Header.Caption.Bounds);
    }

    [Fact]
    public void FileMenuOpen_RequestsDestinationInsteadOfReadingAndKeepsFrostedHeaderActiveOutsideItsBounds()
    {
        using var test = new HeaderFixture(); test.Frame.TitleBarOverContent = true;
        test.Overlays.PopupInsets = new(0, 36, 0, 0);
        using var ui = test.Ui(); ui.Update();
        test.Header.Menu.OpenGroup("file", ui); ui.Update();
        ui.HandleInput(new(InputKind.PointerMove, 700, 400)); test.Clock.Advance(160); ui.Update();
        Assert.True(test.Header.Menu.IsMenuOpen); Assert.True(test.Header.KeepsTitleBarActive);
        Assert.Equal(1f, test.Frame.DefaultTitleBar!.OverlayReveal);
        var body = test.Frame.Content.Bounds;
        DocumentUiFixture.Click(ui, test.Row("document.Open")); ui.Update();
        Assert.Equal(DocumentCommand.Open, test.Documents.PathRequest); Assert.Empty(test.Owner.Store.Reads);
        Assert.Null(test.Overlays.Popup); Assert.Equal(body, test.Frame.Content.Bounds);
        test.Documents.CancelPath(); Assert.Empty(test.Owner.Store.Reads); Assert.Null(test.Documents.Document!.Content);
    }

    private sealed class HintDrawing : IDrawingContext
    {
        public List<(string Text, float Y)> Lines { get; } = [];
        public void Save() { } public void Restore() { } public void Clip(Rect rectangle) { } public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) { } public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) => Lines.Add((text, y));
        public void Image(IImageSource source, Rect destination) { }
    }
}
