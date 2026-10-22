using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;

namespace TrueMoon.Alloy.Hosting
{
    /// <summary>Renderer selected for the built-in integration contract. Custom backends retain their own target contract.</summary>
    public enum UiRendererKind
    {
        /// <summary>No renderer selected.</summary>
        None,
        /// <summary>A caller-supplied backend without built-in standalone capabilities.</summary>
        Custom,
        /// <summary>CPU Skia rendering to RasterUiTarget; external sessions only.</summary>
        Raster,
        /// <summary>Skia rendering to a borrowed desktop OpenGL context.</summary>
        OpenGL,
        /// <summary>Skia rendering to a borrowed Vulkan device.</summary>
        Vulkan
    }

    /// <summary>Graphics target and presenter used by a standalone Silk window.</summary>
    public enum UiWindowGraphicsApi
    {
        /// <summary>Vulkan target and swapchain presenter.</summary>
        Vulkan,
        /// <summary>Desktop OpenGL target and framebuffer presenter.</summary>
        OpenGL
    }

    /// <summary>Explicit renderer/host selection for App integration. Configuration does not allocate GPU resources.</summary>
    public sealed class AlloyOptions
    {
        internal Func<IRenderBackend>? BackendFactory { get; private set; }
        internal bool ExternalHost { get; private set; }
        internal SilkUiWindowOptions? Window { get; private set; }
        /// <summary>Selected built-in renderer, or Custom for UseRenderer. Selection creates no native resources.</summary>
        public UiRendererKind Renderer { get; private set; }
        /// <summary>Selects the Skia Vulkan renderer.</summary>
        public AlloyOptions UseSkiaVulkan() { BackendFactory = () => new SkiaVulkanRenderBackend(); Renderer = UiRendererKind.Vulkan; return this; }
        /// <summary>Selects Vulkan rendering with explicit font files and no system font lookup.</summary>
        /// <param name="fonts">Immutable font snapshots shared by surfaces.</param>
        /// <returns>This configuration for fluent chaining.</returns>
        public AlloyOptions UseSkiaVulkan(SkiaFontCatalog fonts) { ArgumentNullException.ThrowIfNull(fonts); UseSkiaVulkan(); BackendFactory = () => new SkiaVulkanRenderBackend(fonts); return this; }
        /// <summary>Selects the Skia desktop OpenGL renderer on a host-owned context or standalone Silk window.</summary>
        /// <returns>This configuration for fluent chaining.</returns>
        public AlloyOptions UseSkiaOpenGL() { BackendFactory = () => new SkiaOpenGLRenderBackend(); Renderer = UiRendererKind.OpenGL; return this; }
        /// <summary>Selects OpenGL rendering with explicit font files and no system font lookup.</summary>
        /// <param name="fonts">Immutable font snapshots shared by surfaces.</param>
        /// <returns>This configuration for fluent chaining.</returns>
        public AlloyOptions UseSkiaOpenGL(SkiaFontCatalog fonts) { ArgumentNullException.ThrowIfNull(fonts); UseSkiaOpenGL(); BackendFactory = () => new SkiaOpenGLRenderBackend(fonts); return this; }
        /// <summary>Selects in-memory CPU Skia rendering for an external host without a window or Vulkan device.</summary>
        /// <returns>This configuration for fluent chaining.</returns>
        public AlloyOptions UseSkiaRaster() { BackendFactory = () => new SkiaRasterRenderBackend(); Renderer = UiRendererKind.Raster; return this; }
        /// <summary>Selects CPU rendering with explicit font files and no system font lookup.</summary>
        /// <param name="fonts">Immutable font snapshots shared by surfaces.</param>
        /// <returns>This configuration for fluent chaining.</returns>
        public AlloyOptions UseSkiaRaster(SkiaFontCatalog fonts) { ArgumentNullException.ThrowIfNull(fonts); UseSkiaRaster(); BackendFactory = () => new SkiaRasterRenderBackend(fonts); return this; }
        /// <summary>Selects a custom renderer factory.</summary>
        public AlloyOptions UseRenderer(Func<IRenderBackend> factory) { ArgumentNullException.ThrowIfNull(factory); BackendFactory = factory; Renderer = UiRendererKind.Custom; return this; }
        /// <summary>Lets the game supply graphics targets and drive updates/input/composition.</summary>
        public AlloyOptions UseExternalHost() { if (Window != null) throw new InvalidOperationException("A window host is already selected."); ExternalHost = true; return this; }
        /// <summary>Selects a standalone Silk window owned by App hosting.</summary>
        public AlloyOptions UseSilkWindow(Action<SilkUiWindowOptions>? configure = null)
        {
            if (ExternalHost) throw new InvalidOperationException("An external host is already selected.");
            Window = new SilkUiWindowOptions(); configure?.Invoke(Window); return this;
        }
        internal void Validate(bool presentation)
        {
            if (BackendFactory == null) throw new InvalidOperationException("Select a renderer explicitly.");
            if (presentation && Window == null) throw new InvalidOperationException("UsePresentation requires UseSilkWindow.");
            if (presentation && Renderer is not (UiRendererKind.Vulkan or UiRendererKind.OpenGL)) throw new InvalidOperationException("The standalone Silk presenter requires UseSkiaVulkan or UseSkiaOpenGL.");
            if (Window != null)
            {
                Window.ValidateGraphics();
                var expected = Renderer == UiRendererKind.OpenGL ? UiWindowGraphicsApi.OpenGL : UiWindowGraphicsApi.Vulkan;
                if (Window.GraphicsApi is { } requested && requested != expected)
                    throw new InvalidOperationException($"Renderer {Renderer} cannot use the {requested} target/presenter.");
                Window.GraphicsApi = expected;
            }
            if (!presentation && !ExternalHost) throw new InvalidOperationException("UseAlloy requires UseExternalHost; use UsePresentation for a window.");
            if (Window != null && (Window.Width <= 0 || Window.Height <= 0)) throw new ArgumentOutOfRangeException("Window size");
            if (Window != null) { ArgumentNullException.ThrowIfNull(Window.Appearance); Window.Appearance.Validate(); }
            Window?.ValidateChrome();
        }
        internal AlloyOptions Snapshot() => new()
        { BackendFactory = BackendFactory, Renderer = Renderer, ExternalHost = ExternalHost, Window = Window?.Snapshot() };
    }

    /// <summary>Standalone window settings.</summary>
    public sealed class SilkUiWindowOptions
    {
        /// <summary>Explicit target/presenter. Null infers the selected renderer in UsePresentation;
        /// direct HostedUiWindow construction defaults to Vulkan.</summary>
        public UiWindowGraphicsApi? GraphicsApi { get; set; }
        // Retained source compatibility for existing in-repository probes; no independent boolean selection.
        internal bool OpenGL { get => GraphicsApi == UiWindowGraphicsApi.OpenGL; set => GraphicsApi = value ? UiWindowGraphicsApi.OpenGL : UiWindowGraphicsApi.Vulkan; }
        /// <summary>Initial logical width.</summary>
        public int Width { get; set; } = 960;
        /// <summary>Initial logical height.</summary>
        public int Height { get; set; } = 540;
        /// <summary>Window title.</summary>
        public string Title { get; set; } = "TrueMoon UI";
        /// <summary>Enables bounded session counters and CPU timings, including host presentation. Disabled by default.</summary>
        public bool EnableDiagnostics { get; set; }
        /// <summary>Automatic preserves polling for existing FramePresented observers; otherwise frames are on demand.</summary>
        public UiFrameMode FrameMode { get; set; } = UiFrameMode.Automatic;
        /// <summary>Maximum scheduled frame attempts per second (1–240). Native refresh and input layout can run immediately.</summary>
        public int FramesPerSecond { get; set; } = 60;
        /// <summary>Explicit immutable transparency and decoration settings.</summary>
        public WindowAppearance Appearance { get; set; } = new();
        /// <summary>Optional Windows custom frame. Requires an undecorated window.</summary>
        public WindowChromeOptions? Chrome { get; set; }
        /// <summary>Creates Argentis title-bar content on the window owner thread. The window commands remain host-owned.</summary>
        public Func<IWindowCommands, TrueMoon.Argentis.Element>? TitleBarFactory { get; set; }
        /// <summary>Uses the built-in square border/titlebar/grip when Chrome is enabled and no TitleBarFactory is supplied.</summary>
        public bool UseDefaultWindowFrame { get; set; } = true;
        internal void ValidateChrome()
        {
            if (Chrome != null)
            {
                Chrome.Validate(); Chrome.ValidateSize(new TrueMoon.Argentis.Size(Width, Height));
                if (Appearance.Decorated) throw new ArgumentException("Custom chrome requires Decorated=false.");
            }
            if (TitleBarFactory != null && Chrome == null) throw new ArgumentException("TitleBarFactory requires custom Chrome.");
        }
        /// <summary>Optional sink that requires Vulkan core/synchronization validation.</summary>
        public Action<string>? ValidationMessage { get; set; }
        internal void ValidateGraphics()
        {
            if (GraphicsApi is { } api && !Enum.IsDefined(api)) throw new ArgumentOutOfRangeException(nameof(GraphicsApi));
        }
        internal SilkUiWindowOptions Snapshot() => new()
        {
            GraphicsApi = GraphicsApi, Width = Width, Height = Height, Title = Title,
            EnableDiagnostics = EnableDiagnostics, FrameMode = FrameMode, FramesPerSecond = FramesPerSecond,
            Appearance = Appearance, Chrome = Chrome, TitleBarFactory = TitleBarFactory, UseDefaultWindowFrame = UseDefaultWindowFrame, ValidationMessage = ValidationMessage
        };
    }
}
