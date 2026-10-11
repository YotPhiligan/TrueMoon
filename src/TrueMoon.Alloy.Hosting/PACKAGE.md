# TrueMoon.Alloy.Hosting

Optional App integration for Alloy/Argentis UI: standalone OpenGL/Vulkan windows or sessions in an external host. Current native delivery supports **Windows x64 and .NET 10**. This is an alpha API; preparation of local packages does not establish release readiness.

For an application using the Microsoft DI adapter, reference these packages at the same version:

```xml
<PackageReference Include="TrueMoon.Alloy.Hosting" Version="YOUR_VERSION" />
<PackageReference Include="TrueMoon" Version="YOUR_VERSION" />
<PackageReference Include="TrueMoon.Extensions.DependencyInjection" Version="YOUR_VERSION" />
```

Argentis, Alloy, Rendering.Skia and Platform.Silk arrive transitively. The custom Skia DLL is delivered automatically to `truemoon-native/win-x64/libSkiaSharp.dll` in build and publish output; manual copying or a machine-wide Skia installation is unnecessary.

```csharp
using TrueMoon;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Argentis;
using TrueMoon.Extensions.DependencyInjection;

await App.Builder(options => options.UseDI()).RunAsync(app =>
    app.UsePresentation<MainView>(options => options
        .UseSkiaOpenGL()
        .UseSilkWindow(window => window.Title = "My Alloy application")));

public sealed class MainView : View
{
    public MainView() => Content(() => VStack.Create().Padding(16).WithChildren(
        Text.Plain("Hello from Alloy"), Button.Simple("Action").OnClick(() => Console.WriteLine("Clicked"))));
}
```

Select `UseSkiaVulkan()` instead of `UseSkiaOpenGL()` before building the App to use Vulkan. Raster sessions use `UseAlloy(options => options.UseSkiaRaster().UseExternalHost())`, a `RasterUiTarget` and an application-owned frame loop.

Constructors and factories can be mixed. `TextBox.Create().BindTwoWay(TextBox.ValueProperty, model, state => state.Name)` binds an `INotifyPropertyChanged` model to an editor. Keep durable state in the model and create a fresh UI tree when recreating a session.

App configuration/Build do not create windows or graphics contexts. Standalone windows share a process owner thread; the dispatcher handles window creation, input, rendering and teardown. Use `HostedUiWindow.Post(...)` to change its session from another thread and `Close()`/App stop for shutdown. Do not operate on its UI tree or GPU context directly from a worker thread. Hosted window completion follows session/native cleanup.

External-host sessions remain on the caller's owner/render thread. Dispose them there before stopping App from another thread. Sessions own their tree and rendering resources; external GPU devices and the host frame loop remain application-owned. Dispose raster snapshots explicitly. Vulkan texture leases must be returned with their actual final image layout before resize/disposal; GPU queue access is sequential.

Migration: Alloy runtime types remain in `TrueMoon.Alloy`; Silk convenience adapters and standalone presentation live in `TrueMoon.Alloy.Hosting`. The old UI prototype moved to `TrueMoon.Alloy.Hosting.Compatibility` without type forwarders. Rebuild consumers after updating references/imports; binary compatibility across alpha changes is not promised.

Known limit: the current tested NVIDIA ICD retains GDI resources across repeated Vulkan window creation. A successful package smoke does not close K7, prove physical DPI/device-loss behavior, or establish full alpha acceptance. Linux/macOS, ARM64, single-file, trimming, NativeAOT and self-contained deployment are not covered by this package smoke.
