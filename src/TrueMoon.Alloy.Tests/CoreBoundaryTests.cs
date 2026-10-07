using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class CoreBoundaryTests
{
    [Fact]
    public void Alloy_AssemblyReferences_ContainOnlyArgentisAndBcl()
    {
        var references = typeof(UiSession).Assembly.GetReferencedAssemblies();

        Assert.Contains(references, reference => reference.Name == "TrueMoon.Argentis");
        Assert.All(references, reference => Assert.True(reference.Name == "TrueMoon.Argentis"
            || reference.Name?.StartsWith("System.", StringComparison.Ordinal) == true,
            $"Unexpected Alloy dependency: {reference.Name}"));
    }

    [Fact]
    public void Argentis_AssemblyReferences_ContainOnlyBcl()
    {
        var references = typeof(Element).Assembly.GetReferencedAssemblies();

        Assert.All(references, reference => Assert.True(reference.Name?.StartsWith("System.", StringComparison.Ordinal) == true,
            $"Unexpected Argentis dependency: {reference.Name}"));
    }

    [Fact]
    public void Alloy_PublicApi_ExcludesLegacyPresentationTypes()
    {
        var assembly = typeof(UiSession).Assembly;
        var legacyNames = new[] { "Visual", "VisualTree", "VisualTreeBuilder", "IGraphicsPlatform", "IGlGraphicsPlatform",
            "ViewHandle", "ViewManager", "IViewPresenter", "PresentationInitializer", "PresentationConfiguration",
            "AppCreationContextExtensions", "SkiaGlViewPresenter" };

        Assert.DoesNotContain(assembly.GetExportedTypes(), type => legacyNames.Contains(type.Name));
        Assert.Null(assembly.GetType("TrueMoon.Alloy.Hosting.Compatibility.Visual"));
    }
}
