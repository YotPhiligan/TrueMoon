using AlloyTest;
using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class DemoInteractionTests
{
    private static UiSession Create(View1 view, IUiClipboard? clipboard = null, Surface? surface = null) => new(view, surface ?? new Surface(), new UiViewport(800, 1100), clipboard);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\r")]
    [InlineData("\r\n\t")]
    [InlineData("\b")]
    [InlineData("\u001b")]
    [InlineData("\0")]
    [InlineData("\u007f")]
    [InlineData("\u0085")]
    public void ControlOnlyText_PreservesBoundNameGraphemeSelectionAndUnrelatedModelUpdate(string? text)
    {
        var model = new SettingsModel { Name = "Ирина 🧑‍💻", Enabled = false };
        var view = new View1(model);
        using var ui = Create(view);
        ui.Update();
        var editor = view.NameEditor;
        ui.Focus(editor);
        Key(ui, UiKey.End);
        Key(ui, UiKey.Left, shift: true);
        model.Enabled = true;
        ui.Update();
        var notifications = 0;
        var edits = 0;
        model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(SettingsModel.Name)) notifications++; };
        editor.PropertyChanged += (_, property) => { if (ReferenceEquals(property, TextBox.ValueProperty)) edits++; };

        var result = ui.HandleInput(new UiInput(InputKind.Text, Text: text));

        Assert.Equal("Ирина 🧑‍💻", model.Name);
        Assert.Equal("Ирина 🧑‍💻", editor.Value);
        Assert.False(result.Handled);
        Assert.True(model.Enabled);
        Assert.True(view.NotificationToggle.IsChecked);
        Assert.True(editor.IsFocused);
        Assert.Equal("Ирина ".Length, editor.CaretIndex);
        Assert.Equal("Ирина ".Length, editor.SelectionStart);
        Assert.Equal("🧑‍💻".Length, editor.SelectionLength);
        Assert.Equal(0, notifications);
        Assert.Equal(0, edits);
        Assert.False(ui.NeedsUpdate);
    }

    [Theory]
    [InlineData("\r\nЖ\t", "Ж")]
    [InlineData("\0👩‍💻\u007f", "👩‍💻")]
    public void MixedControlAndVisibleText_ReplacesSelectedGraphemeAndWritesFilteredName(string text, string visible)
    {
        var model = new SettingsModel { Name = "Ирина 🧑‍💻" };
        var view = new View1(model);
        using var ui = Create(view);
        ui.Update();
        var editor = view.NameEditor;
        ui.Focus(editor);
        Key(ui, UiKey.End);
        Key(ui, UiKey.Left, shift: true);
        var notifications = 0;
        model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(SettingsModel.Name)) notifications++; };

        Assert.True(ui.HandleInput(new UiInput(InputKind.Text, Text: text)).Handled);

        Assert.Equal("Ирина " + visible, model.Name);
        Assert.Equal(model.Name, editor.Value);
        Assert.Equal(model.Name.Length, editor.CaretIndex);
        Assert.Equal(0, editor.SelectionLength);
        Assert.True(editor.IsFocused);
        Assert.Equal(1, notifications);
        ui.Update();
        Assert.Equal(model.Summary, Assert.IsType<Text>(Assert.Single(Descendants(view).OfType<Border>()).Child).Value);
    }

    [Fact]
    public void AppearanceAndDensity_ChangeExistingTreeWhilePreservingEditorGraphemesFocusCaptureAndScroll()
    {
        var model = new SettingsModel(); for (var i = 0; i < 5; i++) model.AddRow(); model.Rows[2].Name = "A👩‍💻Б";
        var view = new View1(model); using var ui = Create(view); ui.Update(); var instances = Descendants(view).ToArray(); var rows = view.RowList.Items.ToArray();
        var editor = (TextBox)rows[2].Children[0]; ui.Focus(editor); ui.Capture(editor);
        Key(ui, UiKey.End); Key(ui, UiKey.Left, shift: true); Key(ui, UiKey.Left, shift: true); view.RowScroll.Offset = 40; ui.Update();
        Assert.Equal(1, editor.SelectionStart); Assert.Equal("👩‍💻Б".Length, editor.SelectionLength);
        foreach (var appearance in new[] { (Light: true, Compact: false), (Light: true, Compact: true), (Light: false, Compact: true), (Light: false, Compact: false) })
        {
            model.IsLight = appearance.Light; model.IsCompact = appearance.Compact; ui.Update();
            Assert.Equal(model.Appearance, view.Theme); Assert.Equal(model.Appearance.Surface, view.Background);
            Assert.Equal(appearance.Compact ? new Thickness(4) : new Thickness(8), view.NameEditor.Padding);
            Assert.Equal(appearance.Compact ? new Thickness(8, 4, 8, 4) : new Thickness(12, 8, 12, 8), view.LightButton.Padding);
            Assert.Equal(appearance.Compact ? 6 : 12, view.SettingsFields.Spacing); Assert.Equal(8, view.RowList.Spacing);
            Assert.Equal(instances.Length, Descendants(view).Count()); Assert.All(instances.Zip(Descendants(view)), pair => Assert.Same(pair.First, pair.Second));
            Assert.All(rows.Zip(view.RowList.Items), pair => Assert.Same(pair.First, pair.Second));
            Assert.True(editor.IsFocused); Assert.Equal(1, editor.SelectionStart); Assert.Equal("👩‍💻Б".Length, editor.SelectionLength); Assert.Equal(1, editor.CaretIndex);
            Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).PointerCaptured); Assert.Equal(40, view.RowScroll.Offset); Assert.Equal("A👩‍💻Б", model.Rows[2].Name);
        }
        ui.Dispose(); Assert.All(instances, element => { Assert.True(element.IsDisposed); Assert.False(element.IsAttached); });
    }

    [Fact]
    public void DemoKeyboard_ThemeCheckboxSliderAndTabDriveModelAndDisabledFieldsStayInert()
    {
        var model = new SettingsModel(); var view = new View1(model); using var ui = Create(view); ui.Update();
        ui.Focus(view.NameEditor); Key(ui, UiKey.Tab); Assert.True(view.NotificationToggle.IsFocused);
        Activate(ui, view.NotificationToggle, UiKey.Space); Assert.False(model.Enabled); Assert.False(view.NotificationToggle.IsChecked);
        Key(ui, UiKey.Tab); Assert.True(view.VolumeSlider.IsFocused); Key(ui, UiKey.Right); Assert.Equal(.45f, model.Volume, 3); ui.Update(); Assert.Equal(.45f, view.Meter.Value, 3);
        Activate(ui, view.LightButton, UiKey.Enter); Assert.True(model.IsLight); ui.Update(); Assert.Equal(Theme.Light.Surface, view.Background);
        Activate(ui, view.DarkButton, UiKey.Space); Assert.False(model.IsLight); ui.Update(); Assert.Equal(Theme.Dark.Surface, view.Background);
        Activate(ui, view.CompactToggle, UiKey.Space); Assert.True(model.IsCompact); ui.Update(); Assert.Equal(new Thickness(4), view.NameEditor.Padding);
        Activate(ui, view.EditingToggle, UiKey.Space); Assert.False(model.ControlsEnabled); ui.Update(); Assert.False(view.SettingsFields.IsEnabled); Assert.False(view.NameEditor.IsEffectivelyEnabled);
        Assert.Throws<ArgumentException>(() => ui.Focus(view.NameEditor)); var name = model.Name; var volume = model.Volume;
        ui.HandleInput(new UiInput(InputKind.PointerDown, view.NameEditor.Bounds.X + 20, view.NameEditor.Bounds.Y + 10)); ui.HandleInput(new UiInput(InputKind.Text, Text: "blocked"));
        Assert.Equal(name, model.Name); Assert.Equal(volume, model.Volume);
        ui.Focus(view.EditingToggle); Key(ui, UiKey.Tab); Assert.True(Button(view, "Загрузка").IsFocused);
        Activate(ui, view.EditingToggle, UiKey.Enter); ui.Update(); Assert.True(model.ControlsEnabled); Assert.True(view.SettingsFields.IsEnabled); ui.Focus(view.NameEditor); Assert.True(view.NameEditor.IsFocused);
    }

    [Theory]
    [InlineData("AB", "A", "B", "A", "B")]
    [InlineData("АБ", "А", "Б", "А", "Б")]
    [InlineData("A👩‍💻Б", "A👩‍💻", "Б", "A", "👩‍💻Б")]
    [InlineData("é👨‍👩‍👧‍👦", "é", "👨‍👩‍👧‍👦", "é", "👨‍👩‍👧‍👦")]
    public void DemoEditor_LatinCyrillicEmojiAndCombiningClustersRespectClipboardAndEditingBoundaries(string original, string withoutLast, string lastCluster, string firstCluster, string withoutFirst)
    {
        var model = new SettingsModel { Name = original }; var clipboard = new Clipboard(); var view = new View1(model); using var ui = Create(view, clipboard); ui.Update(); var editor = view.NameEditor; ui.Focus(editor);
        Key(ui, UiKey.End); Key(ui, UiKey.Left, shift: true); Assert.Equal(lastCluster.Length, editor.SelectionLength); Key(ui, UiKey.C, control: true); Assert.Equal(lastCluster, clipboard.Text); Assert.Equal(original, model.Name);
        Key(ui, UiKey.End); Key(ui, UiKey.Backspace); Assert.Equal(withoutLast, model.Name); Assert.Equal(withoutLast.Length, editor.CaretIndex); Assert.Equal(0, editor.SelectionLength);
        model.Name = original; ui.Update(); Key(ui, UiKey.Home); Key(ui, UiKey.Delete); Assert.Equal(withoutFirst, model.Name); Assert.Equal(0, editor.CaretIndex);
        model.Name = original; ui.Update(); Key(ui, UiKey.Home); Key(ui, UiKey.Backspace); Assert.Equal(original, model.Name); Assert.Equal(0, editor.CaretIndex);
        Key(ui, UiKey.End); Key(ui, UiKey.Delete); Assert.Equal(original, model.Name); Assert.Equal(original.Length, editor.CaretIndex);
        Key(ui, UiKey.Home); Key(ui, UiKey.Right, shift: true); Assert.Equal(firstCluster.Length, editor.SelectionLength); Key(ui, UiKey.C, control: true); Assert.Equal(firstCluster, clipboard.Text);
        Key(ui, UiKey.A, control: true); Key(ui, UiKey.X, control: true); Assert.Equal(original, clipboard.Text); Assert.Equal("", model.Name); Assert.Equal(0, editor.CaretIndex);
        clipboard.Text = original + "\r\nновый"; Key(ui, UiKey.V, control: true); Assert.Equal(original + "новый", model.Name); Assert.Equal(model.Name, editor.Value); Assert.Equal(editor.Value.Length, editor.CaretIndex);
        Key(ui, UiKey.Home, shift: true); Assert.Equal(0, editor.CaretIndex); Assert.Equal(model.Name.Length, editor.SelectionLength);
        Assert.True(ui.HandleInput(new UiInput(InputKind.Text, Text: "Latin\r\nкириллица\t👩‍💻")).Handled); Assert.Equal("Latinкириллица👩‍💻", model.Name); Assert.Equal(model.Name.Length, editor.CaretIndex); Assert.Equal(0, editor.SelectionLength);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(.25f)]
    [InlineData(1f)]
    public void CustomMeter_UsesSharedVolumeBindingExactFillAndLocalizedDrawing(float volume)
    {
        var model = new SettingsModel { Volume = volume }; var view = new View1(model); using var ui = Create(view); ui.Update();
        Assert.Equal(volume, view.VolumeSlider.Value); Assert.Equal(volume, view.Meter.Value); Assert.Equal(new Size(200, 36), view.Meter.DesiredSize);
        var drawing = new Drawing(); view.Meter.Draw(drawing);
        Assert.Equal(view.Meter.Bounds, drawing.Fills[0].Bounds); Assert.Equal(view.Meter.Bounds with { Width = view.Meter.Bounds.Width * volume }, drawing.Fills[1].Bounds);
        Assert.Equal(view.Theme.Accent, drawing.Fills[1].Color); Assert.Equal($"Громкость: {volume:P0}", Assert.Single(drawing.Texts).Value); Assert.Equal(view.Theme.Foreground, drawing.Texts[0].Color);
        model.Volume = .6f; Assert.Equal(volume, view.Meter.Value); ui.Update(); Assert.Equal(.6f, view.Meter.Value);
        model.ControlsEnabled = false; ui.Update(); drawing = new Drawing(); view.Meter.Draw(drawing); Assert.Equal(view.Theme.Disabled, drawing.Fills[1].Color); Assert.Equal(view.Theme.Disabled, Assert.Single(drawing.Texts).Color);
    }

    [Fact]
    public void RealDemoIcon_RendersPackagedCheckerColorsAndThemeBackgroundThroughSkia()
    {
        var model = new SettingsModel(); var view = new View1(model);
        using var ui = UiSession.Create(view, new SkiaRasterRenderBackend(), new RasterUiTarget(), new UiViewport(800, 1100)); ui.Update();
        Assert.Equal(new Size(32, 32), view.Logo.Source!.Size); Assert.Equal(48, view.Logo.Bounds.Width); Assert.Equal(48, view.Logo.Bounds.Height);
        using var snapshot = ui.SnapshotRasterImage(); using var bitmap = SKBitmap.FromImage(snapshot);
        var x = (int)view.Logo.Bounds.X; var y = (int)view.Logo.Bounds.Y;
        Assert.Equal(new SKColor(93, 153, 255), bitmap.GetPixel(x + 4, y + 4)); Assert.Equal(new SKColor(255, 196, 64), bitmap.GetPixel(x + 16, y + 4));
        Assert.Equal(new SKColor(Theme.Dark.Surface.R, Theme.Dark.Surface.G, Theme.Dark.Surface.B), bitmap.GetPixel(790, 1090));
        model.UseLight(); ui.Update(); using var light = ui.SnapshotRasterImage(); using var lightBitmap = SKBitmap.FromImage(light);
        Assert.Equal(new SKColor(Theme.Light.Surface.R, Theme.Light.Surface.G, Theme.Light.Surface.B), lightBitmap.GetPixel(790, 1090));
        Assert.Equal(new SKColor(93, 153, 255), lightBitmap.GetPixel(x + 4, y + 4)); ui.Dispose(); Assert.True(view.Logo.IsDisposed);
    }

    [Fact]
    public void StaticDemo_DoesNotRedrawAndDisposesEveryRetainedNodeAndOwnedResourceOnce()
    {
        var model = new SettingsModel(); var view = new View1(model); var surface = new Surface(); using var ui = Create(view, surface: surface); ui.Update();
        var nodes = Descendants(view).ToArray(); var resource = new DisposalCounter(); view.Own(resource); var frames = surface.Renders;
        Assert.False(ui.Update()); model.UseDark(); model.IsCompact = false; model.Name = model.Name; model.Volume = model.Volume; model.Enabled = model.Enabled; model.ControlsEnabled = true;
        Assert.False(ui.Update()); Assert.Equal(frames, surface.Renders); Assert.All(nodes.Zip(Descendants(view)), pair => Assert.Same(pair.First, pair.Second));
        ui.Dispose(); ui.Dispose(); Assert.Equal(1, resource.Count); Assert.Equal(1, surface.Disposals); Assert.All(nodes, node => { Assert.True(node.IsDisposed); Assert.False(node.IsAttached); });
        model.Name = "after dispose"; model.UseLight(); model.IsCompact = true; model.AddRow(); Assert.Equal(frames, surface.Renders); Assert.Equal("Алексей", view.NameEditor.Value);
        Assert.Throws<ObjectDisposedException>(() => view.NameEditor.Value = "cannot edit");
    }

    private static IEnumerable<Element> Descendants(Element root) { yield return root; foreach (var child in root.Children) foreach (var descendant in Descendants(child)) yield return descendant; }
    private static Button Button(View1 view, string label) => Descendants(view).OfType<Button>().Single(button => button.Value == label);
    private static void Key(UiSession ui, UiKey key, bool control = false, bool shift = false) => ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key, Control: control, Shift: shift));
    private static void Activate(UiSession ui, Button button, UiKey key) { ui.Focus(button); Key(ui, key); ui.HandleInput(new UiInput(InputKind.KeyUp, Key: key)); }
    private sealed class Clipboard : IUiClipboard { public string? Text { get; set; } public string? GetText() => Text; public void SetText(string text) => Text = text; }
    private sealed class Surface : IUiRenderSurface
    {
        public int Renders { get; private set; } public int Disposals { get; private set; }
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { } public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { root.Draw(new RecordingDrawingContext()); Renders++; }
        public void Dispose() => Disposals++;
    }
    private sealed class Drawing : IDrawingContext
    {
        public List<(Rect Bounds, Color Color)> Fills { get; } = []; public List<(string Value, Color Color)> Texts { get; } = [];
        public void Save() { } public void Restore() { } public void Clip(Rect rectangle) { } public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) => Fills.Add((rectangle, color));
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) => Texts.Add((text, color));
        public void Image(IImageSource source, Rect destination) { }
    }
}
