using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Configuration;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class AlloyIntegrationTests
{
    [Theory]
    [InlineData(UiRendererKind.OpenGL, UiWindowGraphicsApi.OpenGL, true)]
    [InlineData(UiRendererKind.OpenGL, UiWindowGraphicsApi.Vulkan, false)]
    [InlineData(UiRendererKind.Vulkan, UiWindowGraphicsApi.Vulkan, true)]
    [InlineData(UiRendererKind.Vulkan, UiWindowGraphicsApi.OpenGL, false)]
    [InlineData(UiRendererKind.Raster, UiWindowGraphicsApi.OpenGL, false)]
    [InlineData(UiRendererKind.Raster, UiWindowGraphicsApi.Vulkan, false)]
    [InlineData(UiRendererKind.Custom, UiWindowGraphicsApi.OpenGL, false)]
    [InlineData(UiRendererKind.Custom, UiWindowGraphicsApi.Vulkan, false)]
    public void Factory_DeclaresOnlySupportedStandalonePairs(UiRendererKind renderer, UiWindowGraphicsApi api, bool supported)
    {
        using var factory = new HostedUiSessionFactory(Backend(renderer));
        Assert.Equal(renderer, factory.Renderer); Assert.Equal(supported, factory.SupportsWindowGraphics(api));
        Assert.False(factory.SupportsWindowGraphics((UiWindowGraphicsApi)999)); Assert.Equal(0, factory.ActiveSessionCount);
    }

    [Theory]
    [InlineData(UiRendererKind.OpenGL, UiWindowGraphicsApi.Vulkan)]
    [InlineData(UiRendererKind.Vulkan, UiWindowGraphicsApi.OpenGL)]
    [InlineData(UiRendererKind.Raster, UiWindowGraphicsApi.Vulkan)]
    [InlineData(UiRendererKind.Custom, UiWindowGraphicsApi.OpenGL)]
    public async Task UnsupportedWindow_RejectsBeforeRootOrNativeStartup(UiRendererKind renderer, UiWindowGraphicsApi api)
    {
        using var factory = new HostedUiSessionFactory(Backend(renderer)); var roots = 0;
        var window = new HostedUiWindow(new SilkUiWindowOptions { GraphicsApi = api }, factory, () => { roots++; return new Panel(); });
        var error = await Assert.ThrowsAsync<NotSupportedException>(() => window.StartAsync());
        Assert.Contains(api.ToString(), error.Message); Assert.Equal(0, roots); Assert.Equal(0, factory.ActiveSessionCount);
        Assert.Same(error, await Record.ExceptionAsync(() => window.Completion)); Assert.False(window.IsClosed);
        Assert.Throws<ObjectDisposedException>(window.RequestFrame);
    }

    [Fact]
    public async Task UndeclaredCustomWindowFactory_RejectsBeforeCallingCreate()
    {
        var window = new HostedUiWindow(new SilkUiWindowOptions(), new UnknownFactory(), () => throw new Exception("Root called"));
        var error = await Assert.ThrowsAsync<NotSupportedException>(() => window.StartAsync());
        Assert.Contains(nameof(IUiWindowSessionFactory), error.Message);
        Assert.Same(error, await Record.ExceptionAsync(() => window.Completion));
    }

    [Theory]
    [InlineData(UiRendererKind.Raster, UiRendererKind.OpenGL)]
    [InlineData(UiRendererKind.Raster, UiRendererKind.Vulkan)]
    [InlineData(UiRendererKind.OpenGL, UiRendererKind.Raster)]
    [InlineData(UiRendererKind.OpenGL, UiRendererKind.Vulkan)]
    [InlineData(UiRendererKind.Vulkan, UiRendererKind.Raster)]
    [InlineData(UiRendererKind.Vulkan, UiRendererKind.OpenGL)]
    public void WrongTarget_RejectsBeforeHostCallbacksOrRootOwnership(UiRendererKind renderer, UiRendererKind targetKind)
    {
        var callbacks = 0;
        UiRenderTarget target = targetKind switch
        {
            UiRendererKind.Raster => new RasterUiTarget(),
            UiRendererKind.OpenGL => new OpenGLUiTarget(_ => { callbacks++; throw new Exception("GL loader called"); }, () => { callbacks++; throw new Exception("GL context called"); }),
            _ => new VulkanUiTarget(new VulkanHostContext(1, 2, 3, 4, 0, (1u << 22) | (1u << 12),
                (_, _, _) => { callbacks++; throw new Exception("Vulkan loader called"); }, () => { callbacks++; throw new Exception("Vulkan device called"); }, [], []))
        };
        using var factory = new HostedUiSessionFactory(Backend(renderer)); using var root = new Panel();
        var error = Assert.Throws<ArgumentException>(() => factory.Create(root, target, new UiViewport(20, 20)));
        Assert.Equal("target", error.ParamName); Assert.Equal(0, callbacks); Assert.False(root.IsAttached); Assert.False(root.IsDisposed);
        Assert.Equal(0, factory.ActiveSessionCount);
    }

    [Fact]
    public void Snapshot_FreezesRendererAndEveryMutableWindowSetting()
    {
        var original = new AlloyOptions().UseSkiaOpenGL().UseSilkWindow(w => { w.Width = 320; w.Height = 200; w.FrameMode = UiFrameMode.OnDemand; });
        var snapshot = original.Snapshot(); snapshot.Validate(true);
        original.UseSkiaVulkan(); original.Window!.Width = 640; original.Window.Height = 480; original.Window.FrameMode = UiFrameMode.Continuous;
        original.Window.GraphicsApi = UiWindowGraphicsApi.Vulkan; original.Window.Title = "changed";
        Assert.Equal(UiRendererKind.OpenGL, snapshot.Renderer); Assert.IsType<SkiaOpenGLRenderBackend>(snapshot.BackendFactory!());
        Assert.NotSame(original.Window, snapshot.Window); Assert.Equal(320, snapshot.Window!.Width); Assert.Equal(200, snapshot.Window.Height);
        Assert.Equal(UiFrameMode.OnDemand, snapshot.Window.FrameMode); Assert.Equal("TrueMoon UI", snapshot.Window.Title);
        Assert.Equal(UiWindowGraphicsApi.OpenGL, snapshot.Window.GraphicsApi);
    }

    [Fact]
    public void Snapshot_FreezesDefaultTitleBarHookAndItsFrameSelection()
    {
        Action<WindowTitleBar, Element, Action> configure = (_, _, _) => throw new Exception("Configuration must stay lazy");
        var original = new AlloyOptions().UseSkiaOpenGL().UseSilkWindow(w =>
        {
            w.Appearance = new WindowAppearance { Decorated = false };
            w.Chrome = new WindowChromeOptions(); w.ConfigureDefaultTitleBar = configure;
        });
        var snapshot = original.Snapshot(); snapshot.Validate(true);
        original.Window!.ConfigureDefaultTitleBar = (_, _, _) => { };
        original.Window.UseDefaultWindowFrame = false;
        original.Window.TitleBarFactory = _ => throw new Exception("Replacement factory must stay lazy");
        original.Window.Chrome = null;
        Assert.NotSame(original.Window, snapshot.Window);
        Assert.Same(configure, snapshot.Window!.ConfigureDefaultTitleBar);
        Assert.True(snapshot.Window.UseDefaultWindowFrame); Assert.Null(snapshot.Window.TitleBarFactory);
        Assert.NotNull(snapshot.Window.Chrome); Assert.False(snapshot.Window.Appearance.Decorated);
        snapshot.Validate(true);
    }

    [Theory]
    [InlineData("no-chrome", false)]
    [InlineData("custom-factory", false)]
    [InlineData("no-default-frame", false)]
    [InlineData("no-chrome", true)]
    [InlineData("custom-factory", true)]
    [InlineData("no-default-frame", true)]
    public void DefaultTitleBarHook_RejectsIncompatibleFramesBeforeRegistration(string configuration, bool generic)
    {
        var app = new Context(); var roots = 0; var hooks = 0; var titles = 0;
        Action<AlloyOptions> configure = options => options.UseSkiaOpenGL().UseSilkWindow(w =>
            ConfigureFrame(w, configuration, (_, _, _) => hooks++, _ => { titles++; return new Panel(); }));
        var error = Assert.Throws<ArgumentException>(() =>
        {
            if (generic) app.UsePresentation<Panel>(configure);
            else app.UsePresentation(_ => { roots++; return new Panel(); }, configure);
        });
        Assert.Contains("default custom frame", error.Message);
        Assert.Empty(app.Registrations); Assert.Equal(0, roots); Assert.Equal(0, hooks); Assert.Equal(0, titles);
    }

    [Theory]
    [InlineData("no-chrome", false)]
    [InlineData("custom-factory", false)]
    [InlineData("no-default-frame", false)]
    [InlineData("no-chrome", true)]
    [InlineData("custom-factory", true)]
    [InlineData("no-default-frame", true)]
    public void WindowConstructor_OnlyRejectsIncompatibleFramesWhenDefaultTitleBarHookIsPresent(string configuration, bool hasHook)
    {
        var roots = 0; var hooks = 0; var titles = 0; var options = new SilkUiWindowOptions();
        ConfigureFrame(options, configuration, hasHook ? (_, _, _) => hooks++ : null,
            _ => { titles++; return new Panel(); });
        HostedUiWindow Create() => new(options, new UnknownFactory(), () => { roots++; return new Panel(); });
        if (hasHook) Assert.Throws<ArgumentException>(Create);
        else
        {
            var window = Create();
            Assert.False(window.Completion.IsCompleted); window.Close(); Assert.True(window.IsClosed);
        }
        Assert.Equal(0, roots); Assert.Equal(0, hooks); Assert.Equal(0, titles);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Registration_RejectsExplicitPresenterMismatchWithoutServicesOrRoot(bool generic)
    {
        var app = new Context(); var roots = 0;
        Action<AlloyOptions> configure = options => options.UseSkiaOpenGL().UseSilkWindow(w => w.GraphicsApi = UiWindowGraphicsApi.Vulkan);
        var error = Assert.Throws<InvalidOperationException>(() =>
        {
            if (generic) app.UsePresentation<Panel>(configure);
            else app.UsePresentation(_ => { roots++; return new Panel(); }, configure);
        });
        Assert.Contains("target/presenter", error.Message); Assert.Empty(app.Registrations); Assert.Equal(0, roots);
    }

    [Fact]
    public void ExplicitRootRegistration_IsLazyAndValidatesArgumentsBeforeRegistering()
    {
        var app = new Context(); var roots = 0;
        Assert.Same(app, app.UsePresentation(_ => { roots++; return new Panel(); }, o => o.UseSkiaOpenGL().UseSilkWindow()));
        Assert.Equal(0, roots); Assert.Equal(2, app.Registrations.Count);
        var empty = new Context();
        Assert.Throws<ArgumentNullException>(() => empty.UsePresentation(null!, o => o.UseSkiaOpenGL().UseSilkWindow()));
        Assert.Throws<ArgumentNullException>(() => empty.UsePresentation(_ => new Panel(), null!));
        Assert.Throws<ArgumentNullException>(() => AppConfigurationExtensions.UsePresentation(null!, _ => new Panel(), _ => { }));
        Assert.Empty(empty.Registrations);
    }

    [Fact]
    public void InvalidGraphicsEnum_IsRejectedBeforeRegistrationOrNativeStartup()
    {
        var app = new Context();
        Assert.Throws<ArgumentOutOfRangeException>(() => app.UsePresentation(_ => new Panel(), o => o.UseSkiaVulkan().UseSilkWindow(w => w.GraphicsApi = (UiWindowGraphicsApi)999)));
        Assert.Empty(app.Registrations);
        Assert.Throws<ArgumentOutOfRangeException>(() => new HostedUiWindow(new SilkUiWindowOptions { GraphicsApi = (UiWindowGraphicsApi)999 }, new UnknownFactory(), () => new Panel()));
    }

    private static IRenderBackend Backend(UiRendererKind kind) => kind switch
    {
        UiRendererKind.Raster => new SkiaRasterRenderBackend(), UiRendererKind.OpenGL => new SkiaOpenGLRenderBackend(),
        UiRendererKind.Vulkan => new SkiaVulkanRenderBackend(), _ => new CustomBackend()
    };
    private static void ConfigureFrame(SilkUiWindowOptions options, string configuration,
        Action<WindowTitleBar, Element, Action>? hook, Func<IWindowCommands, Element> customFactory)
    {
        options.Appearance = new WindowAppearance { Decorated = false };
        options.Chrome = new WindowChromeOptions(); options.ConfigureDefaultTitleBar = hook;
        switch (configuration)
        {
            case "no-chrome": options.Chrome = null; break;
            case "custom-factory": options.TitleBarFactory = customFactory; break;
            case "no-default-frame": options.UseDefaultWindowFrame = false; break;
            default: throw new ArgumentOutOfRangeException(nameof(configuration));
        }
    }
    private sealed class CustomBackend : IRenderBackend
    { public IUiRenderSurface CreateSurface(UiRenderTarget target, UiViewport viewport) => throw new Exception("Unexpected native allocation"); }
    private sealed class UnknownFactory : IUiSessionFactory
    { public UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null) => throw new Exception("Unexpected Create"); }
    private sealed class Context : IAppConfigurationContext
    {
        internal readonly List<Action<IServicesRegistrationContext>> Registrations = [];
        public IAppConfigurationContext Services(Action<IServicesRegistrationContext> action) { Registrations.Add(action); return this; }
        public IAppConfigurationContext Services(Action<IConfiguration, IServicesRegistrationContext> action) => throw new NotSupportedException();
        public IAppConfigurationContext Configuration(Action<IConfiguration> action) => throw new NotSupportedException();
    }
}
