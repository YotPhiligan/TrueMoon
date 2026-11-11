# TrueMoon.Alloy.Rendering.Skia

Current delivery profile (CORE8, 2026-11-11): **net11.0 / .NET 11**, built with
SDK 11.0.100 and runtime-async=on. Use matching TrueMoon package versions.
Native UI acceptance is bounded to Windows x64; other OS/RID, AOT/trimming,
performance and release gates require separate evidence. Dated net10 results
below remain historical and do not define the current package target.

RichTextEditor uses Topten.RichTextKit transitively through this renderer, with
the same shaping/layout adapter on raster/OpenGL/Vulkan. Argentis public rich-text
contracts remain independent of SkiaSharp. For explicit rich font variants use
SkiaFontCatalog.LoadFaces with licensed normal400/bold700/italic files; missing
variants use the registered normal face. Plain text uses these variants too. Applications deliver their font files.

Parameterless Skia backends use `SkiaFontCatalog.SystemDefault`: the requested
family, `Segoe UI Variable Text`, `Segoe UI`, then the system default. Themes and
new controls request `FontDefaults.Family` (`Segoe UI Variable Text`) at weight400.
System fonts are used in place and are not bundled in the package. Supply a
catalog to add a file fallback or choose another ordered family chain:

```csharp
var fallback = SkiaFontCatalog.Load("AppSans", new Dictionary<string, string>
{
    ["AppSans"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf")
});
var fonts = SkiaFontCatalog.FromSystemFonts(["Segoe UI Variable Text", "Segoe UI"], fallback);
var backend = new SkiaRasterRenderBackend(fonts);
var heading = Text.Plain("Заголовок").FontWeight(600);
var theme = Theme.Dark with { FontFamily = "Segoe UI Variable Text", FontWeight = 400 };
```

Pass the same catalog to `UseSkiaRaster(fonts)`, `UseSkiaOpenGL(fonts)` or
`UseSkiaVulkan(fonts)` when composing `UseAlloy` through App. The existing backend
constructors accept it for direct sessions. Catalogs contain managed data;
each session owns/disposes its native font cache. Fallback family lists are copied.

Selection tries the requested family, then each configured system family, then
the explicit file catalog. With no file catalog it uses the system default.
Use names exposed by the system font manager: on Windows, `Segoe UI Variable Text`
and weight600 select Text Semibold; `Segoe UI Semibold` is not a portable family name.
The closest available weight/italic face is selected. Availability and metrics can
vary by machine. `Load`/`LoadFaces` retain their existing file-only behavior.

`Element`, `Theme`, typed properties and view styles expose `FontWeight`100..900
(400 regular,600 semibold) and `FontItalic`. Measure/draw/cache/caret use the same
selection. RichTextStyle accepts nullable `FontWeight`; an explicit value overrides
`Bold`, while applying a Bold-only formatting patch replaces the explicit weight.
Legacy third-party drawing/text providers can keep the original overloads and use
their normal face until they implement the styled overloads.

Variable named instances are supported through system matching. Arbitrary variation
axes and automatic `opsz` adjustment with font size are not exposed by this API.
The family chain does not add per-glyph shaping/emoji fallback to plain text;
file/custom rich-text catalogs retain replacement-glyph behavior. Explicitly
passing `SystemDefault` preserves the parameterless RichTextKit glyph fallback.
Hinting and
antialiasing policy are independent of this family selection.

Raster, OpenGL and Vulkan backends for Alloy/Argentis. Direct sessions do not require TrueMoon App or Silk window hosting. Current bundled native support is **Windows x64 with .NET 11**; this is an alpha API.

Reference `TrueMoon.Alloy.Rendering.Skia` at the selected local/release package version. Alloy and Argentis arrive transitively. The package's build/buildTransitive hooks copy the extended Skia DLL into `truemoon-native/win-x64/libSkiaSharp.dll` beside the consuming application for both build and publish. The same delivery works through `TrueMoon.Alloy.Hosting`; no manual copy is required.

```csharp
using TrueMoon.Alloy.Runtime;
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

`SkiaOpenGLSurface.CopyTexture(texture, width, height)` copies a borrowed
GL_TEXTURE_2D RGBA8 premultiplied stored-sRGB/top-left texture into a caller-owned
GPU `SkiaImageSource`. Synchronize external production before calling; the method
submits its last source use before returning, including exceptional returns. It
does not take source ownership, wait for GPU completion, read pixels or acknowledge
presentation. The independent copy survives source reuse/destruction. Draw and
dispose it on its original Skia surface/context owner before that surface closes;
raster/foreign-surface drawing and use after disposal are rejected. Keep retained
images bounded by replacing/disposing the previous image on the owner. This is
one GPU copy per image, not zero-copy or a measured performance guarantee.

Migration: raw Vulkan descriptors remain in Rendering.Skia; Silk-specific factories and window presenter types moved to Hosting. Output/native loader paths are unchanged by the transitive package fix. Rebuild alpha consumers after updating assembly references/imports.

Package smoke covers framework-dependent Windows x64 build/publish using .NET 10. Other OS/architectures, single-file, trimming, NativeAOT and self-contained deployment remain unverified. Repeated Vulkan window creation on the current tested NVIDIA ICD has an unresolved GDI resource blocker; package/native delivery is not evidence of full alpha readiness.

BoxShadow uses the same logical geometry on raster/OpenGL/Vulkan. DropShadowOnly
painting excludes the original rounded surface, respects ancestor clips and group
opacity, and disposes per-draw filter/paint resources. No layout or hit-area growth.
Windows x64 source/published Dark/Light100/150/200% gallery passed with frosted menu
surfaces and actual blur pixel witnesses. Native Skia's GENERAL source stage now
includes HOST for its existing HOST_WRITE access mask, fixing VUID02819. The patch
is source-built into the bundled4.155.0 library; ABI/ownership are unchanged. Fresh
package Vulkan Debug/published Release consumers passed validation including teardown.