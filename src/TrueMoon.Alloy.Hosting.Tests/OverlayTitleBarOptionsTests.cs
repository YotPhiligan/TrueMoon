using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Configuration;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class OverlayTitleBarOptionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Snapshot_FreezesOverlaySelectionWithoutCallingRootOrTitleHook(bool overlay)
    {
        var calls = 0;
        var original = new AlloyOptions().UseSkiaOpenGL().UseSilkWindow(window =>
        {
            window.Appearance = new() { Decorated = false };
            window.Chrome = new(); window.TitleBarOverContent = overlay;
            window.ConfigureDefaultTitleBar = (_, _, _) => calls++;
        });
        var snapshot = original.Snapshot();
        original.Window!.TitleBarOverContent = !overlay;
        original.Window.Chrome = null; original.Window.UseDefaultWindowFrame = false;
        snapshot.Validate(true);
        Assert.Equal(overlay, snapshot.Window!.TitleBarOverContent);
        Assert.True(snapshot.Window.UseDefaultWindowFrame); Assert.NotNull(snapshot.Window.Chrome);
        Assert.False(snapshot.Window.Appearance.Decorated); Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("no-chrome", false)]
    [InlineData("custom-title", false)]
    [InlineData("no-frame", false)]
    [InlineData("no-chrome", true)]
    [InlineData("custom-title", true)]
    [InlineData("no-frame", true)]
    public void OverlaySelection_RejectsIncompatibleFramesBeforeRegistrationOrNativeAllocation(string invalid, bool constructor)
    {
        var roots = 0; var titles = 0; var context = new Context();
        void Configure(SilkUiWindowOptions window)
        {
            window.Appearance = new() { Decorated = false }; window.Chrome = new(); window.TitleBarOverContent = true;
            if (invalid == "no-chrome") window.Chrome = null;
            else if (invalid == "custom-title") window.TitleBarFactory = _ => { titles++; return new Panel(); };
            else window.UseDefaultWindowFrame = false;
        }
        var error = Assert.Throws<ArgumentException>(() =>
        {
            if (constructor)
            {
                var options = new SilkUiWindowOptions(); Configure(options);
                _ = new HostedUiWindow(options, new NoNativeFactory(), () => { roots++; return new Panel(); });
            }
            else context.UsePresentation(_ => { roots++; return new Panel(); }, options => options.UseSkiaOpenGL().UseSilkWindow(Configure));
        });
        Assert.Contains(nameof(SilkUiWindowOptions.TitleBarOverContent), error.Message);
        Assert.Empty(context.Registrations); Assert.Equal(0, roots); Assert.Equal(0, titles);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SystemAccentBorder_SnapshotKeepsSelectionAndDefaultIsOptOut(bool accent)
    {
        Assert.False(new SilkUiWindowOptions().UseSystemAccentBorder);
        var original = new AlloyOptions().UseSkiaOpenGL().UseSilkWindow(window =>
        {
            window.Appearance = new() { Decorated = false }; window.Chrome = new(); window.UseSystemAccentBorder = accent;
        });
        var snapshot = original.Snapshot(); original.Window!.UseSystemAccentBorder = !accent;
        snapshot.Validate(true);
        Assert.Equal(accent, snapshot.Window!.UseSystemAccentBorder);
        Assert.True(snapshot.Window.UseDefaultWindowFrame); Assert.NotNull(snapshot.Window.Chrome);
    }

    [Theory]
    [InlineData("no-chrome", false)] [InlineData("custom-title", false)] [InlineData("no-frame", false)]
    [InlineData("no-chrome", true)] [InlineData("custom-title", true)] [InlineData("no-frame", true)]
    public void SystemAccentBorder_RejectsIncompatibleFramesBeforeRegistrationOrActivation(string invalid, bool constructor)
    {
        var roots = 0; var context = new Context();
        void Configure(SilkUiWindowOptions window)
        {
            window.Appearance = new() { Decorated = false }; window.Chrome = new(); window.UseSystemAccentBorder = true;
            if (invalid == "no-chrome") window.Chrome = null;
            else if (invalid == "custom-title") window.TitleBarFactory = _ => throw new InvalidOperationException("No activation.");
            else window.UseDefaultWindowFrame = false;
        }
        var error = Assert.Throws<ArgumentException>(() =>
        {
            if (constructor)
            {
                var options = new SilkUiWindowOptions(); Configure(options);
                _ = new HostedUiWindow(options, new NoNativeFactory(), () => { roots++; return new Panel(); });
            }
            else context.UsePresentation(_ => { roots++; return new Panel(); }, options => options.UseSkiaOpenGL().UseSilkWindow(Configure));
        });
        Assert.Contains(nameof(SilkUiWindowOptions.UseSystemAccentBorder), error.Message);
        Assert.Empty(context.Registrations); Assert.Equal(0, roots);
    }

    private sealed class NoNativeFactory : IUiSessionFactory
    {
        public UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null) =>
            throw new InvalidOperationException("Validation must precede native allocation.");
    }
    private sealed class Context : IAppConfigurationContext
    {
        public List<Action<IServicesRegistrationContext>> Registrations { get; } = [];
        public IAppConfigurationContext Services(Action<IServicesRegistrationContext> action) { Registrations.Add(action); return this; }
        public IAppConfigurationContext Services(Action<IConfiguration, IServicesRegistrationContext> action) => throw new NotSupportedException();
        public IAppConfigurationContext Configuration(Action<IConfiguration> action) => throw new NotSupportedException();
    }
}
