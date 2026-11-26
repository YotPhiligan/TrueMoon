using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Argentis.Styling;
using TrueMoon.ManualTests.AetherEditorHost;
using Xunit;
using Xunit.Abstractions;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

public sealed class EditorVisualStatesTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, 1f)]
    [InlineData(false, 1.25f)]
    [InlineData(false, 1.75f)]
    [InlineData(true, 1f)]
    [InlineData(true, 1.25f)]
    [InlineData(true, 1.75f)]
    public async Task Transform_ClipsDraftWrapsErrorsAndMovesVisibleFocus(bool light, float scale)
    {
        using var fixture = new InspectorFixture();
        var inspector = EditorInspectorView.Create(fixture.Model, new EditorInspectorViewStyle());
        using var ui = InspectorFixture.Ui(inspector, width: 224, height: 480, scale: scale);
        var root = Environment.GetEnvironmentVariable("TRUEMOON_UIQA_RESULTS");
        var name = light ? "light" : "dark";
        var directory = Path.Combine(root ?? Path.GetTempPath(), $"inspector-{name}-{scale * 100:0}-{Guid.NewGuid():N}");
        try
        {
            var before = fixture.Owner.Document.Read().Content;
            await EditorVisualStatesSmoke.RunRasterAsync(ui, inspector, light ? Theme.Light : Theme.Dark, name, directory);
            Assert.Same(before, fixture.Owner.Document.Read().Content);
        }
        finally
        {
            if (root == null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            else output.WriteLine(directory);
        }
    }
}
