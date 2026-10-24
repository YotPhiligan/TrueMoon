using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorStatusBarViewTests
{
    [Theory]
    [InlineData(1f)] [InlineData(1.5f)] [InlineData(2f)]
    public void DefaultFrame_StatusBarReachesBottomBorderWithoutExtraRowAndKeepsResizeGripClear(float scale)
    {
        using var test = new StatusFixture();
        var frame = WindowFrame.CreateDefault("Editor", test.Overlays);
        frame.ApplyWindowState(new(true, false, true, true, true));
        using var ui = new UiSession(frame, new Surface(), new((int)(1280 * scale), (int)(800 * scale), scale));
        ui.Update(); ui.Update();
        var bar = test.View.Bar; var details = test.View.DetailsButton;
        Assert.Equal(new Rect(1, 771, 1278, 28), test.View.Bounds);
        Assert.Equal(test.View.Bounds, bar.Bounds);
        Assert.Equal(new Rect(1263, 783, 16, 16), frame.ResizeGrip!.Bounds);
        Assert.True(details.Bounds.X + details.Bounds.Width <= frame.ResizeGrip.Bounds.X);
        var regions = WindowRegionMap.Create(frame);
        Assert.Equal(WindowRegionRole.ResizeBottomRight, regions.HitTest(1271, 791));
        Assert.Equal(WindowRegionRole.Client, regions.HitTest(1262, 791));
        Assert.Equal(WindowRegionRole.Client, regions.HitTest(details.Bounds.X + 12, details.Bounds.Y + 12));
        frame.ApplyWindowState(new(true, true, true, true, true)); ui.Update();
        Assert.Equal(new Rect(1, 771, 1278, 28), test.View.Bounds);
        Assert.False(frame.ResizeGrip.IsVisible);
        Assert.Equal(WindowRegionRole.Client, WindowRegionMap.Create(frame).HitTest(1271, 791));
        frame.ApplyWindowState(new(true, false, true, true, true));
        ui.Resize(new((int)(1280 * scale), (int)(640 * scale), scale)); ui.SetTheme(Theme.Light); ui.Update(); ui.Update();
        Assert.Equal(new Rect(1, 611, 1278, 28), test.View.Bounds);
        Assert.Equal(test.View.Bounds, bar.Bounds);
        Assert.Equal(new Rect(1263, 623, 16, 16), frame.ResizeGrip.Bounds);
        Assert.Same(bar, test.View.Bar); Assert.Same(details, test.View.DetailsButton);
        Assert.True(details.Bounds.X + details.Bounds.Width <= frame.ResizeGrip.Bounds.X);
        Assert.False(ui.Update());
    }

    [Fact]
    public void CompactSegments_Use28PixelBar12PixelText14PixelSymbolsAndRetainIdentityAcrossThemes()
    {
        using var test = new StatusFixture(); using var ui = test.Ui(); ui.Update();
        var view = test.View; var summary = view.SummaryButton; var preview = view.PreviewButton; var entities = view.Entities;
        Assert.Equal(28, view.Bounds.Height); Assert.Equal(24, view.DetailsButton.Bounds.Width);
        Assert.Equal(24, view.Bar.Padding.Right); // Reserve the native resize grip.
        Assert.All(new Element[] { summary, preview, entities, view.CancelButton }, element => Assert.Equal(12, element.FontSize));
        Assert.False(view.CancelButton.IsVisible); Assert.Equal(0, summary.BorderWidth); Assert.Equal(0, preview.BorderWidth);
        var drawing = new RecordingDrawing(); summary.Draw(drawing);
        var text = Assert.Single(drawing.Texts);
        Assert.Equal(test.Model.Summary, text.Value); Assert.Equal(12, text.Font);
        var area = summary.Bounds.Deflate(summary.Padding);
        var symbol = drawing.Fills.Where(fill => fill.Color != Color.Transparent && fill.Bounds.Width <= 14 && fill.Bounds.Height <= 14).ToArray();
        Assert.NotEmpty(symbol);
        Assert.Equal(12.25f, symbol.Max(fill => fill.Bounds.X + fill.Bounds.Width) - symbol.Min(fill => fill.Bounds.X), 2);
        Assert.All(symbol, fill =>
        {
            Assert.InRange(fill.Bounds.X, area.X, area.X + 14);
            Assert.InRange(fill.Bounds.X + fill.Bounds.Width, area.X, area.X + 14);
            Assert.InRange(fill.Bounds.Y, area.Y + (area.Height - 14) / 2, area.Y + (area.Height + 14) / 2);
        });
        Assert.Equal(area.X + 20, text.X);
        ui.SetTheme(Theme.Light); ui.Update();
        Assert.Same(summary, view.SummaryButton); Assert.Same(preview, view.PreviewButton); Assert.Same(entities, view.Entities);
        Assert.Equal(Theme.Light.Control, view.Background); Assert.Equal(Theme.Light.Foreground, summary.Foreground);
        Assert.Equal(12, summary.FontSize); Assert.False(ui.Update());
    }

    [Fact]
    public void Details_ExposeActualRuntimeAndCountWithoutStartingAnyCommandAndToggleClosed()
    {
        using var test = new StatusFixture(); using var ui = test.Ui(); ui.Update();
        test.Source.Model.Refresh(new(SessionState.Playing, ProcessId: 2608, Tick: 913), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false)); ui.Update();
        DocumentUiFixture.Click(ui, test.View.DetailsButton); ui.Update();
        var popup = Assert.IsType<EditorStatusPopupView>(test.Overlays.Popup);
        Assert.Equal("Сведения о редакторе", popup.Title.Value);
        var rows = DocumentUiFixture.Tree(popup).OfType<Text>().Select(text => text.Value).ToArray();
        Assert.Contains(rows, value => value.Contains("Tick 913") && value.Contains("PID 2608"));
        Assert.Contains("7 объектов", rows); Assert.Contains("Edit Preview: выкл.", rows);
        Assert.False(popup.StartPreview.IsEnabled); Assert.False(popup.StopPreview.IsEnabled);
        Assert.False(popup.Error.IsVisible); Assert.Equal(test.Overlays.Theme.MenuBackdrop, popup.Backdrop);
        Assert.Empty(test.Source.RuntimeCalls); Assert.False(test.Source.Preview.Read().Active);
        DocumentUiFixture.Click(ui, test.View.DetailsButton); ui.Update();
        Assert.Null(test.Overlays.Popup); Assert.True(popup.IsDisposed); Assert.True(test.View.DetailsButton.IsFocused);
    }

    [Theory]
    [InlineData("escape")]
    [InlineData("tab")]
    [InlineData("outside")]
    [InlineData("focus-lost")]
    public void DetailsDismissal_ReleasesPopupAndRestoresOrAdvancesFocus(string dismissal)
    {
        using var test = new StatusFixture(); using var ui = test.Ui(); ui.Update();
        DocumentUiFixture.Click(ui, test.View.DetailsButton); ui.Update();
        var popup = Assert.IsType<EditorStatusPopupView>(test.Overlays.Popup);
        ui.HandleInput(dismissal switch
        {
            "escape" => new(InputKind.KeyDown, Key: UiKey.Escape),
            "tab" => new(InputKind.KeyDown, Key: UiKey.Tab),
            "outside" => new(InputKind.PointerDown, test.BodyButton.Bounds.X + 3, test.BodyButton.Bounds.Y + 3),
            _ => new(InputKind.FocusLost)
        }); ui.Update();
        Assert.Null(test.Overlays.Popup); Assert.True(popup.IsDisposed);
        if (dismissal is "escape" or "outside") Assert.True(test.View.DetailsButton.IsFocused);
        if (dismissal == "tab") Assert.True(test.BodyButton.IsFocused);
        Assert.Empty(test.Source.RuntimeCalls); Assert.False(ui.NeedsUpdate);
    }

    [Fact]
    public void SummaryAndPreviewTriggers_ReplaceOnlyTheirTransientPopupAndNeverExecuteOnFocus()
    {
        using var test = new StatusFixture(openDocument: true); using var ui = test.Ui(); ui.Update();
        DocumentUiFixture.Click(ui, test.View.SummaryButton); ui.Update();
        var first = Assert.IsType<EditorStatusPopupView>(test.Overlays.Popup);
        Assert.True(first.StartPreview.IsFocused); Assert.False(test.Source.Preview.Read().Active);
        DocumentUiFixture.Click(ui, test.View.PreviewButton); ui.Update();
        var second = Assert.IsType<EditorStatusPopupView>(test.Overlays.Popup);
        Assert.True(first.IsDisposed); Assert.NotSame(first, second); Assert.Equal("Edit Preview", second.Title.Value);
        Assert.Same(test.View.PreviewButton, test.Overlays.PopupOwner); Assert.True(second.StartPreview.IsFocused);
        Assert.False(test.Source.Preview.Read().Active); Assert.Empty(test.Source.RuntimeCalls);
    }

    [Fact]
    public void PreviewPopup_StartAndStopUseSharedCommittedPreviewAndKeepThePopupAndFocus()
    {
        using var test = new StatusFixture(openDocument: true); using var ui = test.Ui(); ui.Update();
        test.Source.Transform.Fields[0].Value = "81";
        DocumentUiFixture.Click(ui, test.View.PreviewButton); ui.Update();
        var popup = Assert.IsType<EditorStatusPopupView>(test.Overlays.Popup);
        DocumentUiFixture.Click(ui, popup.StartPreview); DocumentUiFixture.Wait(test.Source.Preview.Pending);
        test.Source.Transform.Refresh(DocumentData.Child.Value.ToString()); ui.Update();
        Assert.Same(popup, test.Overlays.Popup); Assert.True(popup.StartPreview.IsFocused);
        Assert.True(test.Source.Preview.Read().Active); Assert.Equal(7, test.Source.Preview.Read().Presented!.Revision);
        Assert.True(popup.StopPreview.IsEnabled); Assert.Equal("Edit Preview: вкл.", test.View.PreviewButton.Value);
        Assert.Equal("81", test.Source.Transform.Fields[0].Value); Assert.Equal(0, test.Source.Owner.Document.Read().Content!.UndoCount);
        DocumentUiFixture.Click(ui, popup.StopPreview); DocumentUiFixture.Wait(test.Source.Preview.Pending);
        test.Source.Transform.Refresh(DocumentData.Child.Value.ToString()); ui.Update();
        Assert.Same(popup, test.Overlays.Popup);
        Assert.False(test.Source.Preview.Read().Active); Assert.Null(test.Source.Preview.Read().Presented);
        Assert.Equal("Edit Preview: выкл.", test.View.PreviewButton.Value); Assert.False(popup.StopPreview.IsEnabled);
        Assert.Equal("81", test.Source.Transform.Fields[0].Value); Assert.Empty(test.Source.RuntimeCalls);
    }

    [Fact]
    public void CancellationSegment_TracksBusyOperationAndInvokesExistingCommand()
    {
        using var test = new StatusFixture(); using var ui = test.Ui(); ui.Update();
        test.Source.Model.Refresh(new(SessionState.Stopped), EditorWorkspaceTests.Status(GameEditorOperation.Build, false)); ui.Update();
        Assert.True(test.View.CancelButton.IsVisible); Assert.True(test.View.CancelButton.IsEnabled);
        Assert.Equal("Сборка проекта…", test.View.SummaryButton.Value);
        DocumentUiFixture.Click(ui, test.View.CancelButton); DocumentUiFixture.Wait(test.Source.Commands.Pending);
        Assert.Equal(new[] { EditorCommand.Cancel }, test.Source.RuntimeCalls.ToArray());
        test.Source.Refresh(SessionState.Stopped); ui.Update();
        Assert.False(test.View.CancelButton.IsVisible); Assert.False(test.View.CancelButton.IsEnabled);
    }

    [Fact]
    public void ErrorPopup_NormalizesCrLfAndRetainsDiagnosticSelectionFocusAndIdentityAcrossThemeChanges()
    {
        using var test = new StatusFixture();
        test.Source.Model.Refresh(new(SessionState.Faulted), EditorWorkspaceTests.Status(GameEditorOperation.Idle, false),
            new IOException("first complete line\r\nsecond complete line\rthird complete line"));
        using var ui = test.Ui(raster: true); ui.Update();
        Assert.Equal(test.View.Theme.Error, test.View.SummaryButton.Foreground);
        DocumentUiFixture.Click(ui, test.View.DetailsButton); ui.Update();
        var popup = Assert.IsType<EditorStatusPopupView>(test.Overlays.Popup);
        Assert.Equal("Ошибка: first complete line\nsecond complete line\nthird complete line", popup.Error.Text);
        Assert.True(popup.Error.IsVisible); popup.Error.Select(8, 5); ui.Focus(popup.Error);
        var error = popup.Error; var document = error.Document;
        ui.SetTheme(Theme.Light); ui.Update();
        Assert.Same(popup, test.Overlays.Popup); Assert.Same(error, popup.Error); Assert.Same(document, error.Document);
        Assert.Equal(8, error.SelectionStart); Assert.Equal(5, error.SelectionLength); Assert.True(error.IsFocused);
        Assert.Equal(Theme.Light.Error, error.Foreground); Assert.Equal(Theme.Light.MenuBackdrop, popup.Backdrop);
        ui.HandleInput(new(InputKind.Text, Text: "replace diagnostic")); ui.Update();
        Assert.Same(document, error.Document); Assert.Contains("second complete line", error.Text);
    }

    [Fact]
    public void ResponsiveOverflow_PreservesSegmentVisibilityAndMakesItsDetailsAvailable()
    {
        using var test = new StatusFixture(); using var ui = test.Ui(220); ui.Update(); ui.Update();
        Assert.Equal(new[] { test.View.Entities, test.View.PreviewButton }, test.View.Bar.OverflowedItems);
        Assert.True(test.View.Entities.IsVisible); Assert.True(test.View.PreviewButton.IsVisible);
        Assert.True(test.View.DetailsButton.Bounds.Width > 0); Assert.True(test.View.SummaryButton.Bounds.Width > 0);
        DocumentUiFixture.Click(ui, test.View.DetailsButton); ui.Update();
        var popup = Assert.IsType<EditorStatusPopupView>(test.Overlays.Popup);
        Assert.Contains(DocumentUiFixture.Tree(popup).OfType<Text>(), text => text.Value == test.Model.EntityText);
        Assert.Contains(DocumentUiFixture.Tree(popup).OfType<Text>(), text => text.Value == test.Model.PreviewSummary);
    }

    [Fact]
    public void OpenPreviewPopup_WhenItsTriggerOverflowsClosesAndReturnsFocusToDetails()
    {
        using var test = new StatusFixture(openDocument: true); using var ui = test.Ui(); ui.Update();
        DocumentUiFixture.Click(ui, test.View.PreviewButton); ui.Update();
        var popup = Assert.IsType<EditorStatusPopupView>(test.Overlays.Popup); Assert.True(popup.StartPreview.IsFocused);
        ui.Resize(new(220, 800)); ui.Update(); ui.Update();
        Assert.Contains(test.View.PreviewButton, test.View.Bar.OverflowedItems);
        Assert.Null(test.Overlays.Popup); Assert.True(popup.IsDisposed); Assert.True(test.View.DetailsButton.IsFocused);
        Assert.False(test.View.NeedsFrame); Assert.False(test.Source.Preview.Read().Active); Assert.Empty(test.Source.RuntimeCalls);
        ui.Update(); Assert.False(ui.NeedsUpdate);
    }

    [Fact]
    public void PendingOverflowFocus_DoesNotStealFocusFromAReplacementPopup()
    {
        using var test = new StatusFixture(openDocument: true); using var ui = test.Ui(); ui.Update();
        DocumentUiFixture.Click(ui, test.View.PreviewButton); ui.Update();
        var previous = Assert.IsType<EditorStatusPopupView>(test.Overlays.Popup);
        ui.Resize(new(220, 800)); ui.Update(); Assert.True(test.View.NeedsFrame);
        var other = Button.Simple("Other popup");
        test.Overlays.Open(test.BodyButton, other, test.BodyButton.Bounds, ui, test.BodyButton); ui.Focus(other);
        ui.Update(); ui.Update();
        Assert.True(previous.IsDisposed); Assert.Same(other, test.Overlays.Popup); Assert.True(other.IsFocused);
        Assert.False(test.View.DetailsButton.IsFocused); Assert.False(test.View.NeedsFrame);
    }

    [Fact]
    public void DetachingDuringPopupOverflow_CancelsDeferredFocusWithoutDisposingBorrowedModel()
    {
        using var test = new StatusFixture(openDocument: true); using var ui = test.Ui(); ui.Update();
        DocumentUiFixture.Click(ui, test.View.PreviewButton); ui.Update();
        var popup = Assert.IsType<EditorStatusPopupView>(test.Overlays.Popup);
        ui.Resize(new(220, 800)); ui.Update(); Assert.True(test.View.NeedsFrame);
        var content = Assert.IsType<Panel>(test.Overlays.Content); content.Items.Remove(test.View);
        Assert.False(test.View.NeedsFrame); Assert.False(test.View.IsAttached); Assert.False(test.View.IsDisposed);
        ui.Update(); Assert.Null(test.Overlays.Popup); Assert.True(popup.IsDisposed);
        content.Add(test.View); ui.Resize(new(1280, 800)); ui.Update(); ui.Focus(test.BodyButton); ui.Update();
        Assert.True(test.BodyButton.IsFocused); Assert.False(test.View.DetailsButton.IsFocused);
        Assert.False(test.View.NeedsFrame); Assert.True(test.Model.CanPreview); Assert.False(test.Source.Owner.Commands.Read().Closed);
    }

    [Fact]
    public void Disposal_ReleasesPopupAndAllBindingsWithoutDisposingBorrowedModels()
    {
        using var test = new StatusFixture(); var ui = test.Ui(); ui.Update();
        DocumentUiFixture.Click(ui, test.View.DetailsButton); ui.Update();
        var popup = Assert.IsType<EditorStatusPopupView>(test.Overlays.Popup); var summary = test.View.SummaryButton.Value;
        ui.Dispose(); Assert.True(test.View.IsDisposed); Assert.True(popup.IsDisposed); Assert.True(popup.Error.IsDisposed);
        test.Source.Refresh(SessionState.Playing); test.Source.Documents.PathDraft = "still valid.aetherscene";
        Assert.Equal("Симуляция", test.Model.Summary); Assert.Equal(summary, test.View.SummaryButton.Value);
        Assert.False(test.Source.Owner.Commands.Read().Closed); Assert.Equal("still valid.aetherscene", test.Source.Documents.PathDraft);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PopupFactoryFailure_LeavesHostEmptyAndPreservesTriggerFocusAndBorrowedModel(bool nullProduct)
    {
        using var source = new HeaderFixture();
        using var model = new EditorStatusBarViewModel(source.Model, source.Documents, source.Transform);
        using var host = new OverlayHost(new Panel()); var failure = new IOException("popup activation failed");
        using var view = new EditorStatusBarView(model, new EditorStatusBarViewStyle(),
            new Factory<EditorStatusPopupView, EditorStatusPopupArguments>(_ => nullProduct ? null! : throw failure), new(host));
        ((Panel)host.Content).Add(view);
        using var ui = DocumentUiFixture.Ui(host); ui.Update(); ui.Focus(view.DetailsButton);
        var error = Record.Exception(() => DocumentUiFixture.Click(ui, view.DetailsButton));
        if (nullProduct) Assert.IsType<InvalidOperationException>(error); else Assert.Same(failure, error);
        Assert.Null(host.Popup); Assert.True(view.DetailsButton.IsFocused); Assert.False(view.IsDisposed);
        source.Refresh(SessionState.Playing); ui.Update(); Assert.Equal("Симуляция", view.SummaryButton.Value);
    }

    private sealed class StatusFixture : IDisposable
    {
        public HeaderFixture Source { get; }
        public EditorStatusBarViewModel Model { get; }
        public EditorStatusBarView View { get; }
        public OverlayHost Overlays { get; }
        public Button BodyButton { get; } = Button.Simple("Body");
        public StatusFixture(bool openDocument = false)
        {
            Source = new(openDocument: openDocument); Model = new(Source.Model, Source.Documents, Source.Transform);
            var content = new Panel(); Overlays = new(content);
            View = new(Model, new EditorStatusBarViewStyle(),
                new Factory<EditorStatusPopupView, EditorStatusPopupArguments>(args => new(Model, new EditorStatusPopupViewStyle(), args)), new(Overlays))
                { VerticalAlignment = LayoutAlignment.End };
            BodyButton.Width = 80; BodyButton.Height = 30;
            BodyButton.HorizontalAlignment = LayoutAlignment.Start; BodyButton.VerticalAlignment = LayoutAlignment.Start;
            content.Add(BodyButton); content.Add(View);
        }
        public UiSession Ui(int width = 1280, bool raster = false) => raster ?
            UiSession.Create(Overlays, new SkiaRasterRenderBackend(), new RasterUiTarget(), new(width, 800)) :
            new(Overlays, new Surface(), new(width, 800));
        public void Dispose() { Overlays.Dispose(); Model.Dispose(); Source.Dispose(); }
    }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new TextLayout();
        public void VerifyAvailable() { } public void Resize(UiViewport viewport) { } public void Render(Element root, UiViewport viewport) { } public void Dispose() { }
    }
    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
    private sealed class RecordingDrawing : IDrawingContext
    {
        public List<(Rect Bounds, Color Color)> Fills { get; } = [];
        public List<(string Value, float X, float Font)> Texts { get; } = [];
        public void Save() { } public void Restore() { } public void Clip(Rect rectangle) { } public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) => Fills.Add((rectangle, color));
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) => Texts.Add((text, x, fontSize));
        public void Image(IImageSource source, Rect destination) { }
    }
}
