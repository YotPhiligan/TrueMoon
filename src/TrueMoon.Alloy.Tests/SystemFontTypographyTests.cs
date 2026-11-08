using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class SystemFontTypographyTests
{
    [Fact]
    public void FrameworkDefaults_SelectVariableTextForThemesAndNewControls()
    {
        Assert.Equal("Segoe UI Variable Text", FontDefaults.Family);
        Assert.Equal("Segoe UI", FontDefaults.FallbackFamily);
        Assert.Equal("Segoe UI Variable Text", Theme.Dark.FontFamily);
        Assert.Equal("Segoe UI Variable Text", Theme.Light.FontFamily);
        Assert.Equal("Segoe UI Variable Text", UiProperties.FontFamily.DefaultValue);
        using var text = new Text(); using var editor = new TextBox(); using var rich = new RichTextEditor();
        Assert.Equal("Segoe UI Variable Text", text.FontFamily); Assert.Equal(400, text.FontWeight);
        Assert.Equal("Segoe UI Variable Text", editor.FontFamily); Assert.Equal("Segoe UI Variable Text", rich.FontFamily);
    }

    [Fact]
    public void ExplicitSystemDefault_PreservesImplicitRichTextUnicodeAndCaretPositions()
    {
        using var implicitSurface = new SkiaRasterRenderBackend().CreateSurface(new RasterUiTarget(), new(400, 100));
        using var explicitSurface = new SkiaRasterRenderBackend(SkiaFontCatalog.SystemDefault).CreateSurface(new RasterUiTarget(), new(400, 100));
        using var implicitResources = new SkiaDrawingResources(); using var explicitResources = new SkiaDrawingResources(SkiaFontCatalog.SystemDefault);
        const string value = "a😀b";
        var style = new Topten.RichTextKit.Style { FontFamily = FontDefaults.Family, FontSize = 16 };
        Assert.Equal(value, implicitResources.RichFonts.PrepareText(value, style));
        Assert.Equal(value, explicitResources.RichFonts.PrepareText(value, style));
        using var normal = ((IRichTextLayoutService)implicitSurface.TextLayout).CreateRichTextLayout(RichTextDocument.FromText(value), new(), null, RichTextAlignment.Left);
        using var configured = ((IRichTextLayoutService)explicitSurface.TextLayout).CreateRichTextLayout(RichTextDocument.FromText(value), new(), null, RichTextAlignment.Left);
        Assert.Equal(normal.Size, configured.Size); Assert.Equal(normal.GetCaretBounds(4), configured.GetCaretBounds(4));
        Assert.Equal(4, configured.GetLineBoundary(0, true));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(600)]
    public void DefaultSystemPolicy_PlainAndRichTextSelectTheSameAvailableFamilyAndWeight(int weight)
    {
        SkiaNativeLibrary.Initialize();
        var family = new[] { "Segoe UI Variable Text", "Segoe UI" }.FirstOrDefault(candidate =>
            SKFontManager.Default.FontFamilies.Contains(candidate, StringComparer.OrdinalIgnoreCase));
        using var style = new SKFontStyle(weight, 5, SKFontStyleSlant.Upright);
        using var expected = SKTypeface.FromFamilyName(family, style);
        using var resources = new SkiaDrawingResources();
        var plain = resources.Font(FontDefaults.Family, 16, weight).Font;
        var rich = resources.RichFonts.TypefaceFromStyle(new Topten.RichTextKit.Style
            { FontFamily = FontDefaults.Family, FontWeight = weight }, false);
        using var unknown = SkiaFontCatalog.SystemDefault.CreateTypeface("TrueMoon-unavailable-default-family", weight, false);
        Assert.Equal(expected.FamilyName, plain.Typeface.FamilyName); Assert.Equal(expected.FontWeight, plain.Typeface.FontWeight);
        Assert.Equal(expected.FamilyName, rich.FamilyName); Assert.Equal(expected.FontWeight, rich.FontWeight);
        Assert.Equal(expected.FamilyName, unknown.FamilyName);
    }

    private static SkiaFontCatalog Files() => SkiaFontCatalog.Load("Fixed", new Dictionary<string, string>
    { ["Fixed"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf") });

    [SystemFontFact("Segoe UI")]
    public void SystemChain_UsesRequestedFamilyBeforeOrderedFallbacksAndSnapshotsOptions()
    {
        var families = new List<string> { "TrueMoon-missing-font", "Segoe UI", "Consolas" };
        var catalog = SkiaFontCatalog.FromSystemFonts(families, Files());
        families.Clear(); families.Add("Consolas");
        using var fallback = catalog.CreateTypeface("TrueMoon-missing-request", 600, false);
        using var requested = catalog.CreateTypeface("Segoe UI", 400, true);
        Assert.Equal("Segoe UI", fallback.FamilyName); Assert.Equal(600, fallback.FontWeight);
        Assert.Equal("Segoe UI", requested.FamilyName); Assert.Equal(400, requested.FontWeight); Assert.True(requested.IsItalic);
        var other = SkiaFontCatalog.FromSystemFonts(["TrueMoon-missing-font"], Files());
        using var explicitRequested = other.CreateTypeface("Segoe UI", 600, true);
        Assert.Equal("Segoe UI", explicitRequested.FamilyName); Assert.Equal(600, explicitRequested.FontWeight); Assert.True(explicitRequested.IsItalic);
    }

    [SystemFontFact("Segoe UI")]
    public void FileCatalog_IsExplicitAndSystemCatalogUsesFileFallbackOnlyAfterSystemCandidates()
    {
        var files = Files();
        using var explicitFace = files.CreateTypeface("Segoe UI", 600, false);
        using var missing = SkiaFontCatalog.FromSystemFonts(["TrueMoon-missing-font"], files).CreateTypeface("not-installed", 600, false);
        using var system = SkiaFontCatalog.FromSystemFonts(["Segoe UI"], files).CreateTypeface("Fixed", 600, false);
        using var fileFace = files.CreateTypeface("Fixed");
        Assert.Equal("Noto Sans", explicitFace.FamilyName); Assert.Equal(fileFace.FontWeight, explicitFace.FontWeight);
        Assert.Equal("Noto Sans", missing.FamilyName);
        Assert.Equal("Segoe UI", system.FamilyName); Assert.Equal(600, system.FontWeight);
    }

    [Fact]
    public void EmptySystemChain_UsesExplicitFileAliasOrSystemDefault()
    {
        using var explicitFace = SkiaFontCatalog.FromSystemFonts([], Files()).CreateTypeface("Fixed", 600, false);
        Assert.Equal("Noto Sans", explicitFace.FamilyName);
        using var style = new SKFontStyle(600, 5, SKFontStyleSlant.Italic);
        using var expected = SKTypeface.FromFamilyName(null, style);
        using var actual = SkiaFontCatalog.FromSystemFonts(["TrueMoon-missing-font"]).CreateTypeface("not-installed", 600, true);
        Assert.Equal(expected.FamilyName, actual.FamilyName); Assert.Equal(expected.FontWeight, actual.FontWeight);
        Assert.Equal(expected.FontSlant, actual.FontSlant);
    }

    [Fact]
    public void SystemChain_RejectsInvalidFamiliesAndRecursiveSystemFallback()
    {
        Assert.Throws<ArgumentNullException>(() => SkiaFontCatalog.FromSystemFonts(null!));
        foreach (var family in new[] { "", " " })
            Assert.Throws<ArgumentException>(() => SkiaFontCatalog.FromSystemFonts([family]));
        Assert.Throws<ArgumentNullException>(() => SkiaFontCatalog.FromSystemFonts([null!]));
        var system = SkiaFontCatalog.FromSystemFonts([]);
        Assert.Throws<ArgumentException>(() => SkiaFontCatalog.FromSystemFonts([], system));
    }

    [SystemFontFact("Segoe UI Variable Text")]
    public void VariableText_SelectsRealRegularAndSemiboldInstancesWithCyrillicGlyphs()
    {
        var catalog = SkiaFontCatalog.FromSystemFonts(["Segoe UI"]);
        using var regular = catalog.CreateTypeface("Segoe UI Variable Text", 400, false);
        using var semibold = catalog.CreateTypeface("Segoe UI Variable Text", 600, false);
        Assert.Equal("Segoe UI Variable Text", regular.FamilyName); Assert.Equal(400, regular.FontWeight);
        Assert.Equal("Segoe UI Variable Text", semibold.FamilyName); Assert.Equal(600, semibold.FontWeight);
        Assert.Equal(2, semibold.VariationDesignParameterCount);
        using var normalFont = new SKFont(regular, 16); using var boldFont = new SKFont(semibold, 16);
        Assert.DoesNotContain((ushort)0, boldFont.GetGlyphs("Aa Привет Ёй"));
        Assert.NotEqual(normalFont.MeasureText("Привет WWW iii"), boldFont.MeasureText("Привет WWW iii"));
    }

    [SystemFontFact("Segoe UI")]
    public void FontCache_SeparatesWeightAndItalicAndDisposesEvictedFonts()
    {
        using var resources = new SkiaDrawingResources(SkiaFontCatalog.FromSystemFonts([]));
        var regular = resources.Font("Segoe UI", 16).Font;
        var semibold = resources.Font("Segoe UI", 16, 600).Font;
        var italic = resources.Font("Segoe UI", 16, 600, true).Font;
        Assert.Same(semibold, resources.Font("Segoe UI", 16, 600).Font);
        Assert.NotSame(regular, semibold); Assert.NotSame(semibold, italic);
        Assert.Equal(400, regular.Typeface.FontWeight); Assert.Equal(600, semibold.Typeface.FontWeight);
        Assert.True(italic.Typeface.IsItalic);
        for (var size = 17; size <= 32; size++) resources.Font("Segoe UI", size, 600);
        Assert.Equal(IntPtr.Zero, regular.Handle); Assert.Equal(IntPtr.Zero, semibold.Handle); Assert.Equal(IntPtr.Zero, italic.Handle);
        var replacement = resources.Font("Segoe UI", 16, 600, true).Font;
        resources.Dispose(); Assert.Equal(IntPtr.Zero, replacement.Handle);
    }

    [Theory]
    [InlineData(400, false, 1f)]
    [InlineData(600, false, 1.5f)]
    [InlineData(600, true, 2f)]
    public void StyledMeasureAndDraw_MatchNativeFaceMetricsAndPixels(int weight, bool italic, float scale)
    {
        SkiaNativeLibrary.Initialize();
        using var actual = SKSurface.Create(new SKImageInfo(500, 140));
        using var expected = SKSurface.Create(new SKImageInfo(500, 140));
        using var resources = new SkiaDrawingResources(SkiaFontCatalog.FromSystemFonts(["Segoe UI"], Files()));
        var drawing = new SkiaDrawingContext(actual.Canvas, resources);
        using var style = new SKFontStyle(weight, 5, italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        using var face = SKFontManager.Default.MatchFamily("Segoe UI", style);
        using var font = new SKFont(face, 16);
        const string text = "Aa Mg Привет Ёй 012345";
        Assert.Equal(weight, face.FontWeight); Assert.Equal(italic, face.IsItalic);
        Assert.Equal(new Size(font.MeasureText(text), font.Metrics.Descent - font.Metrics.Ascent),
            drawing.Measure(text, 16, "TrueMoon-missing-font", weight, italic));
        using var label = new Text(text) { FontFamily = "TrueMoon-missing-font", FontSize = 16,
            FontWeight = weight, FontItalic = italic, Foreground = new(235, 240, 245) };
        label.Measure(new(240, 80), drawing); label.Arrange(new(4, 8, 240, 80));
        Assert.Equal(font.MeasureText(text), label.DesiredSize.Width);
        actual.Canvas.Clear(SKColors.Transparent); expected.Canvas.Clear(SKColors.Transparent);
        var empty = Pixels(actual);
        actual.Canvas.Scale(scale); expected.Canvas.Scale(scale);
        label.Draw(drawing);
        using var paint = new SKPaint { Color = new(235, 240, 245), IsAntialias = true };
        expected.Canvas.ClipRect(new SKRect(4, 8, 244, 88));
        expected.Canvas.DrawText(text, 4, 8 - font.Metrics.Ascent, SKTextAlign.Left, font, paint);
        Assert.Equal(Pixels(expected), Pixels(actual));
        Assert.False(empty.SequenceEqual(Pixels(actual)));
    }

    [SystemFontFact("Segoe UI")]
    public void ThemeWeight_RelayoutsTextAndPreservesFocusedTextBoxSelectionAndHitTesting()
    {
        var label = new Text("WWW iii Привет");
        var editor = new TextBox { Value = "WWWWiiii" };
        var root = new VStack().WithChildren(label, editor);
        using var ui = UiSession.Create(root, new SkiaRasterRenderBackend(), new RasterUiTarget(), new(500, 160));
        ui.SetTheme(Theme.Dark with { FontFamily = "Segoe UI" });
        ui.Update(); ui.Focus(editor);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        var previous = label.DesiredSize.Width;
        root.ApplyTheme(Theme.Dark with { FontFamily = "Segoe UI", FontWeight = 600, FontItalic = true });
        Assert.True(ui.NeedsUpdate); ui.Update();
        Assert.NotEqual(previous, label.DesiredSize.Width);
        Assert.True(editor.IsFocused); Assert.Equal("WWWWiiii", editor.Value); Assert.Equal(8, editor.SelectionLength);
        using var style = new SKFontStyle(600, 5, SKFontStyleSlant.Italic);
        using var face = SKFontManager.Default.MatchFamily("Segoe UI", style); using var font = new SKFont(face, editor.FontSize);
        Assert.Equal(font.MeasureText(label.Value), label.DesiredSize.Width);
        ui.HandleInput(new(InputKind.PointerDown, X: editor.Bounds.X + editor.Padding.Left + font.MeasureText("WWWW"),
            Y: editor.Bounds.Y + editor.Bounds.Height / 2, Button: 0));
        Assert.Equal(4, editor.CaretIndex); Assert.Equal(0, editor.SelectionLength);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(901)]
    public void InvalidWeight_RejectsLocalStyleThemeAndRichFormattingWithoutInvalidation(int weight)
    {
        using var text = new Text("unchanged") { FontWeight = 600 };
        var invalidations = 0; text.Invalidated += _ => invalidations++;
        Assert.Throws<ArgumentOutOfRangeException>(() => text.FontWeight = weight);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(UiProperties.FontWeight, weight));
        Assert.Throws<ArgumentOutOfRangeException>(() => Theme.Dark with { FontWeight = weight });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RichTextStyle { FontWeight = weight });
        Assert.Equal(600, text.FontWeight); Assert.Equal(0, invalidations);
        using var resources = new SkiaDrawingResources(Files());
        Assert.Throws<ArgumentOutOfRangeException>(() => resources.Font("Fixed", 16, weight));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(900)]
    public void BoundaryWeights_AreAcceptedAndInvalidateLayout(int weight)
    {
        using var text = new Text(); var invalidations = new List<Invalidation>(); text.Invalidated += invalidations.Add;
        text.FontWeight(weight);
        Assert.Contains(Invalidation.Layout, invalidations);
        text.Clear(UiProperties.FontWeight); Assert.Equal(400, text.FontWeight);
    }

    [SystemFontFact("Segoe UI")]
    public void RichText_ResolvesInheritedWeightItalicAndLegacyBoldOverrides()
    {
        using var surface = new SkiaRasterRenderBackend().CreateSurface(new RasterUiTarget(), new(400, 100));
        var service = Assert.IsAssignableFrom<IRichTextLayoutService>(surface.TextLayout);
        Size Measure(RichTextStyle run, RichTextStyle defaults)
        {
            using var layout = service.CreateRichTextLayout(new RichTextDocument([new("WWW iii Привет", run)]),
                defaults with { FontFamily = "Segoe UI", FontSize = 18 }, null, RichTextAlignment.Left);
            return layout.Size;
        }
        var normal = Measure(new(), new() { FontWeight = 400 });
        var semibold = Measure(new(), new() { FontWeight = 600, Italic = true });
        Assert.NotEqual(normal, semibold);
        Assert.Equal(semibold, Measure(new() { FontWeight = 600, Italic = true }, new() { FontWeight = 400 }));
        Assert.Equal(normal, Measure(new() { Bold = false, Italic = false }, new() { FontWeight = 600, Italic = true }));
        Assert.Equal(Measure(new() { FontWeight = 700 }, new()), Measure(new() { Bold = true }, new() { FontWeight = 600 }));
        Assert.Equal(semibold, Measure(new() { FontWeight = 600, Bold = true, Italic = true }, new()));
    }

    [Fact]
    public void RichFormatting_WeightAndBoldReplaceEachOtherAndUndoRetainsExplicitWeight()
    {
        var editor = new RichTextEditor { Document = new([new("draft", new() { FontWeight = 600 })]) };
        using var ui = UiSession.Create(editor, new SkiaRasterRenderBackend(Files()), new RasterUiTarget(), new(400, 100));
        ui.Update(); ui.Focus(editor); editor.Select(0, 5);
        editor.ApplyStyle(new() { Bold = true });
        Assert.Null(editor.Document.Runs[0].Style.FontWeight); Assert.True(editor.Document.Runs[0].Style.Bold);
        Assert.True(editor.Undo()); Assert.Equal(600, editor.Document.Runs[0].Style.FontWeight);
        editor.ApplyStyle(new() { FontWeight = 400 });
        Assert.Null(editor.Document.Runs[0].Style.Bold); Assert.Equal(400, editor.Document.Runs[0].Style.FontWeight);
        Assert.Equal("draft", editor.Text); Assert.Equal(5, editor.SelectionLength);
    }

    [Fact]
    public void RichKeyboardFormatting_TogglesExplicitAndInheritedBoldItalicWithoutReplacingDocumentText()
    {
        var editor = new RichTextEditor { Text = "draft", FontWeight = 700, FontItalic = true };
        using var ui = UiSession.Create(editor, new SkiaRasterRenderBackend(Files()), new RasterUiTarget(), new(400, 100));
        ui.Update(); ui.Focus(editor); editor.SelectAll();
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.B, Control: true));
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.I, Control: true));
        Assert.False(editor.Document.Runs[0].Style.Bold); Assert.False(editor.Document.Runs[0].Style.Italic);
        editor.ApplyStyle(new() { FontWeight = 700 });
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.B, Control: true));
        Assert.Null(editor.Document.Runs[0].Style.FontWeight); Assert.False(editor.Document.Runs[0].Style.Bold);
        Assert.Equal("draft", editor.Text); Assert.Equal(5, editor.SelectionLength);
    }

    [SystemFontFact("Consolas")]
    public void RichFontCache_DistinguishesSystemFallbackPoliciesAcrossSurfaces()
    {
        using var first = new SkiaDrawingResources(SkiaFontCatalog.FromSystemFonts(["Segoe UI"]));
        using var second = new SkiaDrawingResources(SkiaFontCatalog.FromSystemFonts(["Consolas"]));
        var style = new Topten.RichTextKit.Style { FontFamily = "TrueMoon-missing-font", FontWeight = 600 };
        var sans = first.RichFonts.TypefaceFromStyle(style, false);
        var mono = second.RichFonts.TypefaceFromStyle(style, false);
        Assert.Equal("Segoe UI", sans.FamilyName); Assert.Equal("Consolas", mono.FamilyName);
        Assert.NotSame(sans, mono);
    }

    [Fact]
    public void FileVariants_ApplyToPlainMeasurementAndDrawingAsWellAsRichText()
    {
        var fonts = SkiaFontCatalog.LoadFaces("Fixed", [
            new("Fixed", Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf")),
            new("Fixed", Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSansMono-Regular.ttf"), 600)]);
        using var resources = new SkiaDrawingResources(fonts);
        var regular = resources.Font("Fixed", 18).Font; var variant = resources.Font("Fixed", 18, 600).Font;
        Assert.Equal("Noto Sans", regular.Typeface.FamilyName); Assert.Equal("Noto Sans Mono", variant.Typeface.FamilyName);
        Assert.NotEqual(regular.MeasureText("iiii"), regular.MeasureText("WWWW"));
        Assert.Equal(variant.MeasureText("iiii"), variant.MeasureText("WWWW"));
    }

    [Fact]
    public void LegacyTextProviders_StyledOverloadsForwardToMatchingNormalMeasurementAndDrawing()
    {
        var legacy = new LegacyTextProvider();
        ITextLayoutService measure = legacy; IDrawingContext draw = legacy;
        Assert.Equal(new Size(30, 15), measure.Measure("abc", 15, "legacy", 600, true));
        draw.Text("abc", 2, 3, new(10, 20, 30), 15, "legacy", 600, true);
        Assert.Equal("abc/15/legacy", legacy.Measured); Assert.Equal("abc/2/3/15/legacy", legacy.Drawn);
    }

    private sealed class LegacyTextProvider : ITextLayoutService, IDrawingContext
    {
        internal string? Measured, Drawn;
        public Size Measure(string text, float size, string family) { Measured = $"{text}/{size}/{family}"; return new(text.Length * 10, size); }
        public void Text(string text, float x, float y, Color color, float size, string family) => Drawn = $"{text}/{x}/{y}/{size}/{family}";
        public void Save() { }
        public void Restore() { }
        public void Clip(Rect rectangle) { }
        public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) { }
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) { }
        public void Image(IImageSource source, Rect destination) { }
    }

    private static byte[] Pixels(SKSurface surface)
    {
        using var image = surface.Snapshot(); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}

/// <summary>System-font integration cases explicitly skip when the named optional family is not installed.</summary>
public sealed class SystemFontFactAttribute : FactAttribute
{
    public SystemFontFactAttribute(string family)
    {
        SkiaNativeLibrary.Initialize();
        if (!SKFontManager.Default.FontFamilies.Contains(family, StringComparer.OrdinalIgnoreCase))
            Skip = $"System font '{family}' is not installed.";
    }
}
