using TrueMoon.Alloy.Rendering.Skia;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class RendererBoundaryTests
{
    [Fact]
    public void Renderer_AssemblyReferences_ExcludeWindowPlatformAndHosting()
    {
        var references = typeof(SkiaVulkanRenderBackend).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name == "TrueMoon.Alloy.Platform.Silk");
        Assert.DoesNotContain(references, reference => reference.Name == "TrueMoon.Alloy.Hosting");
        Assert.DoesNotContain(references, reference => reference.Name?.StartsWith("Silk.NET.Windowing", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void VulkanTarget_PublicApi_AcceptsOnlyRawHostDescriptor()
    {
        var constructor = Assert.Single(typeof(VulkanUiTarget).GetConstructors());
        var parameter = Assert.Single(constructor.GetParameters());

        Assert.Equal(typeof(VulkanHostContext), parameter.ParameterType);
        Assert.Null(typeof(VulkanUiTarget).GetProperty("Device"));
        Assert.Null(typeof(VulkanHostContext).GetMethod("FromSilkDevice"));
        Assert.Throws<ArgumentNullException>(() => new VulkanUiTarget(null!));
    }

    [Fact]
    public void Renderer_PublicApi_ExcludesWindowPresenterAndSilkSurfaceOverload()
    {
        var assembly = typeof(SkiaVulkanRenderBackend).Assembly;
        var constructor = Assert.Single(typeof(VulkanInteropSurface).GetConstructors());

        Assert.Equal(typeof(VulkanHostContext), constructor.GetParameters()[0].ParameterType);
        Assert.DoesNotContain(assembly.GetExportedTypes(), type => type.Name == "VulkanWindowPresenter" || type.Name == "VulkanPresentationResources");
    }
}
