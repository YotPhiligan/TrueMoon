using SkiaSharp;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Hosting;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class SkiaFontCatalogTests
{
    private static string FontFile(string name = "NotoSans-Regular.ttf") => Path.Combine(AppContext.BaseDirectory, "Assets/Fonts", name);
    private static SkiaFontCatalog Catalog() => SkiaFontCatalog.Load("Sans", new Dictionary<string, string>
    { ["Sans"] = FontFile(), ["Mono"] = FontFile("NotoSansMono-Regular.ttf") });

    [Fact]
    public void Catalog_UsesRegisteredFacesAndExplicitDefaultForUnknownOrdinalAliases()
    {
        var catalog = Catalog();
        using var sans = catalog.CreateTypeface("Sans"); using var mono = catalog.CreateTypeface("Mono");
        using var unknown = catalog.CreateTypeface("definitely-not-installed"); using var caseMismatch = catalog.CreateTypeface("mono");
        Assert.Equal("Noto Sans", sans.FamilyName); Assert.Equal("Noto Sans Mono", mono.FamilyName);
        Assert.Equal("Noto Sans", unknown.FamilyName); Assert.Equal("Noto Sans", caseMismatch.FamilyName);
        using var sansFont = new SKFont(sans, 20); using var monoFont = new SKFont(mono, 20);
        Assert.NotEqual(sansFont.MeasureText("iiii"), sansFont.MeasureText("WWWW"));
        Assert.Equal(monoFont.MeasureText("iiii"), monoFont.MeasureText("WWWW"));
    }

    [Fact]
    public void Catalog_SnapshotsFilesAndAliasDictionaryBeforeLaterMutation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoonFont-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "font.ttf");
        try
        {
            File.Copy(FontFile(), path);
            var aliases = new Dictionary<string, string> { ["Fixed"] = path };
            var catalog = SkiaFontCatalog.Load("Fixed", aliases);
            aliases["Fixed"] = FontFile("NotoSansMono-Regular.ttf"); aliases.Clear(); File.Delete(path);
            using var face = catalog.CreateTypeface("Fixed");
            Assert.Equal("Noto Sans", face.FamilyName);
            using var unknown = catalog.CreateTypeface("unknown"); Assert.Equal("Noto Sans", unknown.FamilyName);
        }
        finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(directory); }
    }

    [Fact]
    public void Catalog_ExplicitSymbolAliasContainsSharedSceneArrowGlyph()
    {
        var catalog = SkiaFontCatalog.Load("Sans", new Dictionary<string, string>
        { ["Sans"] = FontFile(), ["Segoe UI Symbol"] = FontFile("NotoSansSymbols-Regular.ttf") });
        using var face = catalog.CreateTypeface("Segoe UI Symbol"); using var font = new SKFont(face, 16);
        Assert.DoesNotContain((ushort)0, font.GetGlyphs("↑"));
        Assert.True(font.MeasureText("↑") > 0);
    }

    [Fact]
    public void Catalog_RejectsMissingDefaultAndCaseInsensitiveDefaultMismatch()
    {
        Assert.Throws<ArgumentException>(() => SkiaFontCatalog.Load("Missing", new Dictionary<string, string> { ["Sans"] = FontFile() }));
        Assert.Throws<ArgumentException>(() => SkiaFontCatalog.Load("sans", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Sans"] = FontFile() }));
    }

    [Fact]
    public void Catalog_RejectsInvalidFontDataAndMissingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "TrueMoonInvalidFont-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(path, [1, 2, 3, 4]);
            var error = Assert.Throws<ArgumentException>(() => SkiaFontCatalog.Load("Fixed", new Dictionary<string, string> { ["Fixed"] = path }));
            Assert.Contains("Could not decode font", error.Message);
            File.Delete(path);
            Assert.Throws<FileNotFoundException>(() => SkiaFontCatalog.Load("Fixed", new Dictionary<string, string> { ["Fixed"] = path }));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Catalog_RejectsBlankAliasesAndPaths(string blank)
    {
        Assert.Throws<ArgumentException>(() => SkiaFontCatalog.Load(blank, new Dictionary<string, string>()));
        Assert.Throws<ArgumentException>(() => SkiaFontCatalog.Load("Fixed", new Dictionary<string, string> { ["Fixed"] = blank }));
        Assert.Throws<ArgumentException>(() => SkiaFontCatalog.Load("Fixed", new Dictionary<string, string> { ["Fixed"] = FontFile(), [blank] = FontFile() }));
    }

    [Fact]
    public void FontCache_EvictionAndDisposalReleaseNativeFontsWithoutInvalidatingCatalog()
    {
        var catalog = Catalog(); using var first = new SkiaDrawingResources(catalog); using var second = new SkiaDrawingResources(catalog);
        var original = first.Font("Sans", 16).Font;
        Assert.Same(original, first.Font("Sans", 16).Font);
        var independent = second.Font("Sans", 16).Font; Assert.NotSame(original, independent);
        for (var size = 17; size <= 32; size++) first.Font("Sans", size);
        Assert.Equal(IntPtr.Zero, original.Handle); Assert.NotEqual(IntPtr.Zero, independent.Handle);
        var replacement = first.Font("Sans", 16).Font;
        using var expectedFace = SKTypeface.FromFile(FontFile()); using var expectedFont = new SKFont(expectedFace, 16);
        Assert.Equal(expectedFont.MeasureText("Привет WWW"), replacement.MeasureText("Привет WWW"));
        first.Dispose(); first.Dispose(); Assert.Equal(IntPtr.Zero, replacement.Handle);
        Assert.NotEqual(IntPtr.Zero, independent.Handle);
        Assert.Throws<ObjectDisposedException>(() => first.Font("Sans", 16));
        using var afterDispose = catalog.CreateTypeface("Sans"); Assert.Equal("Noto Sans", afterDispose.FamilyName);
        second.Dispose(); Assert.Equal(IntPtr.Zero, independent.Handle);
    }

    [Fact]
    public void RasterBackend_UsesCatalogForMeasureAcrossResizeAndIndependentSessions()
    {
        var backend = new SkiaRasterRenderBackend(Catalog());
        using var first = (SkiaRasterSurface)backend.CreateSurface(new RasterUiTarget(), new UiViewport(100, 60));
        using var second = (SkiaRasterSurface)backend.CreateSurface(new RasterUiTarget(), new UiViewport(100, 60));
        using var face = SKTypeface.FromFile(FontFile("NotoSansMono-Regular.ttf")); using var font = new SKFont(face, 20);
        var metrics = font.Metrics;
        var expected = new TrueMoon.Argentis.Size(font.MeasureText("Привет"), metrics.Descent - metrics.Ascent);
        Assert.Equal(expected, first.TextLayout.Measure("Привет", 20, "Mono"));
        first.Resize(new UiViewport(0, 0)); first.Resize(new UiViewport(200, 120, 2));
        Assert.Equal(expected, first.TextLayout.Measure("Привет", 20, "Mono"));
        first.Dispose(); Assert.Equal(expected, second.TextLayout.Measure("Привет", 20, "Mono"));
        Assert.Throws<ObjectDisposedException>(() => first.TextLayout.Measure("Привет", 20, "Mono"));
    }

    [Fact]
    public void BackendConstructors_RejectNullCatalog()
    {
        Assert.Throws<ArgumentNullException>(() => new SkiaRasterRenderBackend(null!));
        Assert.Throws<ArgumentNullException>(() => new SkiaOpenGLRenderBackend(null!));
        Assert.Throws<ArgumentNullException>(() => new SkiaVulkanRenderBackend(null!));
        Assert.Throws<ArgumentNullException>(() => SkiaFontCatalog.Load("Fixed", null!));
        Assert.Throws<ArgumentNullException>(() => SkiaFontCatalog.Load(null!, new Dictionary<string, string>()));
    }

    [Fact]
    public void Catalog_SharedAcrossThreadsCreatesIndependentOwnedNativeFonts()
    {
        var catalog = Catalog();
        using var resources = new SkiaDrawingResources(catalog);
        var owner = resources.Font("Mono", 20).Font;
        IntPtr workerHandle = IntPtr.Zero; string? family = null; Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var worker = new SkiaDrawingResources(catalog);
                var font = worker.Font("Mono", 20).Font; workerHandle = font.Handle;
                using var face = catalog.CreateTypeface("Mono"); family = face.FamilyName;
                worker.Dispose(); Assert.Equal(IntPtr.Zero, font.Handle);
            }
            catch (Exception error) { failure = error; }
        });
        thread.Start(); thread.Join();
        Assert.Null(failure); Assert.Equal("Noto Sans Mono", family);
        Assert.NotEqual(IntPtr.Zero, workerHandle); Assert.NotEqual(owner.Handle, workerHandle);
        Assert.NotEqual(IntPtr.Zero, owner.Handle);
    }

    [Theory]
    [InlineData("raster")]
    [InlineData("opengl")]
    [InlineData("vulkan")]
    public void HostingSelectors_KeepRendererAndStandaloneHostSelectionWithCatalog(string renderer)
    {
        var catalog = Catalog(); var options = new AlloyOptions();
        if (renderer == "raster") options.UseSkiaRaster(catalog).UseExternalHost();
        else
        {
            if (renderer == "opengl") options.UseSkiaOpenGL(catalog); else options.UseSkiaVulkan(catalog);
            options.UseSilkWindow();
        }
        options.Validate(renderer != "raster");
        var backend = options.BackendFactory!();
        if (renderer == "opengl") { Assert.IsType<SkiaOpenGLRenderBackend>(backend); Assert.True(options.Window!.OpenGL); }
        else if (renderer == "vulkan") { Assert.IsType<SkiaVulkanRenderBackend>(backend); Assert.False(options.Window!.OpenGL); }
        else
        {
            Assert.IsType<SkiaRasterRenderBackend>(backend); Assert.True(options.ExternalHost); Assert.Null(options.Window);
            using var surface = backend.CreateSurface(new RasterUiTarget(), new UiViewport(100, 60));
            using var face = SKTypeface.FromFile(FontFile("NotoSansMono-Regular.ttf")); using var font = new SKFont(face, 20);
            Assert.Equal(font.MeasureText("Привет"), surface.TextLayout.Measure("Привет", 20, "Mono").Width);
        }
    }

    [Fact]
    public void HostingSelectors_NullCatalogCannotReplacePreviouslySelectedRenderer()
    {
        var options = new AlloyOptions().UseSkiaRaster(Catalog()).UseExternalHost();
        var factory = options.BackendFactory;
        Assert.Throws<ArgumentNullException>(() => options.UseSkiaVulkan(null!));
        Assert.Throws<ArgumentNullException>(() => options.UseSkiaOpenGL(null!));
        Assert.Throws<ArgumentNullException>(() => options.UseSkiaRaster(null!));
        Assert.Same(factory, options.BackendFactory); options.Validate(false);
        Assert.IsType<SkiaRasterRenderBackend>(options.BackendFactory!());
    }
}
