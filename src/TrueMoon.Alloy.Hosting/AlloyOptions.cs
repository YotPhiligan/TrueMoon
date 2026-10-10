using TrueMoon.Alloy.Rendering.Skia;

namespace TrueMoon.Alloy.Hosting
{
    /// <summary>Explicit renderer/host selection for App integration. Configuration does not allocate GPU resources.</summary>
    public sealed class AlloyOptions
    {
        internal Func<IRenderBackend>? BackendFactory { get; private set; }
        internal bool ExternalHost { get; private set; }
        internal SilkUiWindowOptions? Window { get; private set; }
        private bool _skiaVulkan;
        private bool _skiaOpenGL;
        /// <summary>Selects the Skia Vulkan renderer.</summary>
        public AlloyOptions UseSkiaVulkan() { BackendFactory = () => new SkiaVulkanRenderBackend(); _skiaVulkan = true; _skiaOpenGL = false; return this; }
        /// <summary>Selects the Skia desktop OpenGL renderer on a host-owned context or standalone Silk window.</summary>
        /// <returns>This configuration for fluent chaining.</returns>
        public AlloyOptions UseSkiaOpenGL() { BackendFactory = () => new SkiaOpenGLRenderBackend(); _skiaOpenGL = true; _skiaVulkan = false; return this; }
        /// <summary>Selects in-memory CPU Skia rendering for an external host without a window or Vulkan device.</summary>
        /// <returns>This configuration for fluent chaining.</returns>
        public AlloyOptions UseSkiaRaster() { BackendFactory = () => new SkiaRasterRenderBackend(); _skiaVulkan = false; _skiaOpenGL = false; return this; }
        /// <summary>Selects a custom renderer factory.</summary>
        public AlloyOptions UseRenderer(Func<IRenderBackend> factory) { ArgumentNullException.ThrowIfNull(factory); BackendFactory = factory; _skiaVulkan = false; _skiaOpenGL = false; return this; }
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
            if (presentation && !_skiaVulkan && !_skiaOpenGL) throw new InvalidOperationException("The standalone Silk presenter requires UseSkiaVulkan or UseSkiaOpenGL.");
            if (Window != null) Window.OpenGL = _skiaOpenGL;
            if (!presentation && !ExternalHost) throw new InvalidOperationException("UseAlloy requires UseExternalHost; use UsePresentation for a window.");
            if (Window != null && (Window.Width <= 0 || Window.Height <= 0)) throw new ArgumentOutOfRangeException("Window size");
            if (Window != null) { ArgumentNullException.ThrowIfNull(Window.Appearance); Window.Appearance.Validate(); }
            Window?.ValidateChrome();
        }
    }

    /// <summary>Standalone window settings.</summary>
    public sealed class SilkUiWindowOptions
    {
        internal bool OpenGL { get; set; }
        /// <summary>Initial logical width.</summary>
        public int Width { get; set; } = 960;
        /// <summary>Initial logical height.</summary>
        public int Height { get; set; } = 540;
        /// <summary>Window title.</summary>
        public string Title { get; set; } = "TrueMoon UI";
        /// <summary>Explicit immutable transparency and decoration settings.</summary>
        public WindowAppearance Appearance { get; set; } = new();
        /// <summary>Optional Windows custom frame. Requires an undecorated window.</summary>
        public WindowChromeOptions? Chrome { get; set; }
        /// <summary>Creates Argentis title-bar content on the window owner thread. The window commands remain host-owned.</summary>
        public Func<IWindowCommands, TrueMoon.Argentis.Element>? TitleBarFactory { get; set; }
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
    }
}
