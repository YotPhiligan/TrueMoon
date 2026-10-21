using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class UiPresentationBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Presentation_TracksOnlyTheOutputBoundaryAndPreservesResult(bool presented)
    {
        using var ui = Create(); var diagnostics = ui.EnableDiagnostics(); ui.Update();
        var before = diagnostics.Snapshot;
        Assert.Equal(presented, UiPresentationBoundary.Run(ui, () => presented));
        var after = diagnostics.Snapshot;
        Assert.Equal(1, after.Present.Attempts); Assert.Equal(presented ? 1 : 0, after.Present.Completed);
        Assert.Equal(presented ? 0 : 1, after.Present.Skipped); Assert.Equal(0, after.Present.Failed);
        Assert.Equal(before.Update, after.Update); Assert.Equal(before.Render, after.Render);
    }

    [Fact]
    public void Presentation_ObservesFailureWithoutReplacingTheOriginalExceptionOrFaultingSessionItself()
    {
        using var ui = Create(); var diagnostics = ui.EnableDiagnostics(); var error = new Exception("output");
        Assert.Same(error, Assert.Throws<Exception>(() => UiPresentationBoundary.Run(ui, () => throw error)));
        Assert.Equal(1, diagnostics.Snapshot.Present.Failed); Assert.Equal(0, diagnostics.Snapshot.Present.Completed);
        Assert.Equal(UiSessionState.Active, diagnostics.Snapshot.State); Assert.Equal(0, diagnostics.Snapshot.Update.Attempts);
    }

    [Fact]
    public void DiagnosticsOff_DoesNotCreateACollectorOrAlterOutputErrors()
    {
        using var ui = Create(); var error = new Exception("output");
        Assert.True(UiPresentationBoundary.Run(ui, () => true));
        Assert.Same(error, Assert.Throws<Exception>(() => UiPresentationBoundary.Run(ui, () => throw error)));
        Assert.Null(ui.Diagnostics);
    }

    private static UiSession Create() => new(new Panel(), new Surface(), new UiViewport(20, 20));
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new Layout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { }
        public void Dispose() { }
    }
    private sealed class Layout : ITextLayoutService
    {
        public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize);
    }
}
