// using SkiaSharp;
// using TrueMoon.Argentis;
//
// namespace TrueMoon.Alloy;
//
// public class SkiaVulkanViewPresenter : IViewPresenter
// {
//     private GRVulkanInterface? _grVkInterface;
//     private GRContext? _grContext;
//     private SKSurface? _surface;
//     private SKCanvas? _canvas;
//     private IContentPresenterContext? _contentPresenterContext;
//     private VkDevice? _vkDevice;
//     private VkQueue? _vkQueue;
//     private VkSemaphore? _imageAvailableSemaphore;
//     private VkSemaphore? _renderFinishedSemaphore;
//
//     public void Initialize(IGraphicsPlatform platform)
//     {
//         var view = platform.GetNativeWindow()!;
//         
//         // Создаём Vulkan backend для Skia
//         _grVkInterface = GRVulkanInterface.Create((name =>
//         {
//             var addr = view.VulkanContext!.TryGetProcAddress(name, out var ptr) ? ptr : 0;
//             return addr;
//         }));
//         
//         _grContext = GRContext.CreateVulkan(_grVkInterface);
//         
//         // Создаём render target из Vulkan swapchain
//         RecreateRenderTarget(view.FramebufferSize.X, view.FramebufferSize.Y);
//         
//         _contentPresenterContext = new SkiaContentPresenterContext(_canvas);
//     }
//
//     private void RecreateRenderTarget(int width, int height)
//     {
//         // Получаем Vulkan image из swapchain
//         var vkImage = GetVulkanSwapchainImage();
//         
//         // Создаём Skia render target, привязанный к Vulkan framebuffer
//         var renderTarget = new GRBackendRenderTarget(
//             width, height,
//             0, 8,
//             new GRVkFramebufferInfo((uint)vkImage, VK_IMAGE_LAYOUT_PRESENT_SRC_KHR)
//         );
//         
//         _surface = SKSurface.Create(_grContext, renderTarget, 
//             GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
//         _canvas = _surface.Canvas;
//     }
//
//     public void Present(double dt, IVisualTree visualTree)
//     {
//         _grContext.ResetContext();
//         
//         // 2D UI через Skia (напрямую в Vulkan)
//         _canvas.Clear(SKColors.Transparent);
//         
//         // Рисуем UI элементы
//         foreach (var visual in visualTree)
//         {
//             if (_contentPresenterContext == null) break;
//             visual.ContentPresenter?.Present(dt, _contentPresenterContext);
//         }
//         
//         // Flush — команды отправляются в Vulkan
//         _canvas.Flush();
//         
//         // Signal semaphore для Vulkan
//         SignalRenderFinishedSemaphore();
//         
//         // SwapBuffers
//         SwapBuffers();
//     }
//
//     public void Resize(int width, int height)
//     {
//         RecreateRenderTarget(width, height);
//     }
//
//     public void Release()
//     {
//         _surface?.Dispose();
//         _grContext?.Dispose();
//         _grVkInterface?.Dispose();
//     }
// }
