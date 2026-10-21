using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Rendering.Skia;

namespace TrueMoon.Alloy.Hosting;

internal static class UiRendererCompatibility
{
    internal static UiRendererKind Identify(IRenderBackend backend) => backend switch
    {
        SkiaRasterRenderBackend => UiRendererKind.Raster,
        SkiaOpenGLRenderBackend => UiRendererKind.OpenGL,
        SkiaVulkanRenderBackend => UiRendererKind.Vulkan,
        _ => UiRendererKind.Custom
    };

    internal static bool SupportsWindow(UiRendererKind renderer, UiWindowGraphicsApi api) =>
        (renderer, api) is (UiRendererKind.OpenGL, UiWindowGraphicsApi.OpenGL) or (UiRendererKind.Vulkan, UiWindowGraphicsApi.Vulkan);
}
