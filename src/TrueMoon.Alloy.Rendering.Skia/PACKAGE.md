# TrueMoon.Alloy.Rendering.Skia

Raster, OpenGL and Vulkan backends for Alloy/Argentis. Direct sessions do not require TrueMoon App or Silk window hosting. Current bundled native support is **Windows x64 with .NET 10**; this is an alpha API.

Reference `TrueMoon.Alloy.Rendering.Skia` at the selected local/release package version. Alloy and Argentis arrive transitively. The package's build/buildTransitive hooks copy the extended Skia DLL into `truemoon-native/win-x64/libSkiaSharp.dll` beside the consuming application for both build and publish. The same delivery works through `TrueMoon.Alloy.Hosting`; no manual copy is required.

```csharp
using TrueMoon.Alloy;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

using var ui = UiSession.Create(Text.Plain("Hello from a package"),
    new SkiaRasterRenderBackend(), new RasterUiTarget(), new UiViewport(480, 260, 1));
ui.Update();
using var image = ui.SnapshotRasterImage();
using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
using var output = System.IO.File.Create("ui.png");
data.SaveTo(output);
```

`Update` applies pending changes; static UI does not continuously redraw. Viewport dimensions are physical pixels and layout/input coordinates are logical units through its scale. Sessions and snapshots have explicit ownership: dispose the session on its creator thread and dispose each caller-owned snapshot. No await or thread switch should occur while making session calls unless access returns to its owner thread.

The loader automatically uses the bundled DLL for all three backends. `SkiaNativeLibrary.LoadedPath` reports the loaded file. `Initialize(path)` is only needed for a deliberately selected alternative library before the first native call; managed SkiaSharp/native revisions and Vulkan interop ABI must match. Both SkiaSharp and the Vulkan bindings share one native library handle for the process lifetime.

OpenGL/Vulkan targets borrow an application-owned context/device. Keep that host alive until UI disposal. Vulkan texture leases must be explicitly returned with their final image layout, and resize/dispose while borrowed is rejected. For App/standalone windows use the optional `TrueMoon.Alloy.Hosting` package.

Migration: raw Vulkan descriptors remain in Rendering.Skia; Silk-specific factories and window presenter types moved to Hosting. Output/native loader paths are unchanged by the transitive package fix. Rebuild alpha consumers after updating assembly references/imports.

Package smoke covers framework-dependent Windows x64 build/publish using .NET 10. Other OS/architectures, single-file, trimming, NativeAOT and self-contained deployment remain unverified. Repeated Vulkan window creation on the current tested NVIDIA ICD has an unresolved GDI resource blocker; package/native delivery is not evidence of full alpha readiness.
