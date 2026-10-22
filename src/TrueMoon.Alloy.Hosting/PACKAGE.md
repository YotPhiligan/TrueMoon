# TrueMoon.Alloy.Hosting

Controls use the square Dark/Light style by default. `Button.Tone` selects
Neutral/Accent/Positive/Danger; `HasError`, `TextBox.IsReadOnly`, inside borders
and keyboard focus share semantic theme tokens. `session.SetTheme(Theme.Light)`
preserves editor/selection/scroll state. Local properties override Style, then Theme.
For the built-in window frame, set `window.Appearance = new WindowAppearance
{ Decorated = false }` and `window.Chrome = new WindowChromeOptions { NativeDrag =
true }`. The host composes the title, window buttons and resize grip; normal windows
keep system decorations. `TitleBarFactory` overrides this composition;
`UseDefaultWindowFrame=false` leaves a caller-composed root untouched. Default close
uses the existing cancellable asynchronous close coordinator.

Explicit root factory: `context.UsePresentation(resolver => new MyView(resolver.Resolve<MyModel>()), configure)`
creates the root on the UI owner after the target is ready. Register dependencies
explicitly; the view is not registered/resolved as a service and a factory-based
Cobalt graph needs no generated view resolver. The generic overload remains.
For an injected factory, register `IFactory<MyView>` and call
`UsePresentationFactory<MyView>(configure)`. It creates each root on the UI owner
thread and transfers ownership to the window. Session/lifecycle interface facades
borrow their target, avoiding multiple provider-owned disposable aliases.
Renderer/window settings are snapshotted during registration. Return a fresh,
detached root; null/disposed/parented/attached roots fault startup without disposing
another owner's tree. Factory exceptions remain observable through Completion.

`UiRendererKind` exposes the selected built-in integration. Raster accepts RasterUiTarget
for external sessions; OpenGL/OpenGLUiTarget and Vulkan/VulkanUiTarget use matching
`UiWindowGraphicsApi` target/presenter pairs. Null window GraphicsApi infers the selected
renderer in UsePresentation; direct HostedUiWindow still defaults to Vulkan.
An explicit mismatch fails before registration/native allocation. Standalone custom
session factories must implement `IUiWindowSessionFactory` and declare compatible
graphics before window allocation; HostedUiSessionFactory does this for built-in
Skia GPU renderers. Custom external IUiSessionFactory remains supported.

Asynchronous close: `RequestCloseAsync(token)` joins a cancellable user attempt and
returns true only after successful cleanup, false on veto. `Closing` handlers
(`Func<UiCloseReason, Task<bool>>`) start sequentially on the owner and may save/drain
asynchronously; marshal UI work after await. Native close uses the same path.
`Close`, `StopAsync` and App Stop ignore vetoes while awaiting every handler;
errors fault `Completion` after cleanup and never set `IsClosed`. `IsCloseRequested`
describes pending work, `CloseError` retains handler errors including native ones.
Caller cancellation affects only its wait. Do not await window/dispatcher shutdown
from a Closing handler or block the owner. The legacy constructor `closed` callback
runs after a cleanup attempt including failure; use Completion for success.

Frame scheduling: `SilkUiWindowOptions.FrameMode` defaults to `UiFrameMode.Automatic`
(on-demand without FramePresented observers, compatibility polling with observers).
Choose `OnDemand` or `Continuous` explicitly; `FramesPerSecond` is1–240/default60.
`HostedUiWindow.RequestFrame()` and `UiSession.RequestFrame()` coalesce dynamic
redraw requests without command capacity. Native expose may present cached pixels;
minimized/zero-sized windows retain redraw until resume. Live windows still poll
OS events with a bounded wait. `NeedsRender` guards output freshness independently
of pending commands/future requests included in `NeedsUpdate`.

Optional App integration for Alloy/Argentis UI: standalone OpenGL/Vulkan windows or sessions in an external host. Current native delivery supports **Windows x64 and .NET 10**. This is an alpha API; preparation of local packages does not establish release readiness.

Diagnostics are opt-in: use `session.EnableDiagnostics()` for direct/external sessions,
or `UseSilkWindow(w => w.EnableDiagnostics = true)` for standalone windows.
`HostedUiWindow.Diagnostics` remains available after cleanup. Its thread-safe
`Snapshot` contains session/window IDs, lifetime/viewport, queue outcomes and separate
Update/measure/arrange/render/present CPU timing aggregates. Storage is fixed, with no
frame history. Update includes nested phases; presentation acceptance is not GPU
completion. External hosts can report output through `RecordPresentation`.

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

Cobalt is the default provider. To use it, keep the Hosting/App references, omit `UseDI()` and register the concrete consumer view with the TrueMoon registration API so the bundled Cobalt analyzer sees its constructor graph:

```csharp
await App.Builder(_ => { }).RunAsync(app =>
{
    app.UsePresentation<MainView>(options => options.UseSkiaOpenGL().UseSilkWindow());
    app.Services(services => services.Transient<MainView>());
});
```

Register the view's model/dependencies in the same `Services` configuration. The analyzer comes transitively from `TrueMoon.Cobalt`; no standalone generator package is needed. The Microsoft DI path also works with `EnableCobaltGenerator=false`.

For reproducible font selection, load your licensed font files with `SkiaFontCatalog.Load(defaultFamily, files)` from `TrueMoon.Alloy.Rendering.Skia` and pass the catalog to `UseSkiaRaster(fonts)`, `UseSkiaOpenGL(fonts)` or `UseSkiaVulkan(fonts)`. Deliver those files and their license as application content with `CopyToOutputDirectory` and `CopyToPublishDirectory`. Loading validates fonts through native Skia before App startup; the renderer package does not bundle demo fonts. Unregistered font families use the catalog's default face without system font lookup.

Constructors and factories can be mixed. `TextBox.Create().BindTwoWay(TextBox.ValueProperty, model, state => state.Name)` binds an `INotifyPropertyChanged` model to an editor. Keep durable state in the model and create a fresh UI tree when recreating a session.

`RichTextEditor.Create(new RichTextEditorOptions { WordWrap = true, UndoLimit = 100 })`
provides multiline text, immutable `RichTextDocument`/`RichTextRun` styling,
selection, plain-text clipboard, undo/redo and read-only options. Bind
`RichTextEditor.DocumentProperty` for rich content or `TextProperty` for LF plain text.
Use `Select(start, length)` and `ApplyStyle(new RichTextStyle { Bold = true })` for
formatting; indices are UTF-16 grapheme boundaries. Topten.RichTextKit arrives
transitively through Rendering.Skia. A toolbar is ordinary application composition.

App configuration/Build do not create windows or graphics contexts. Standalone windows share a process owner thread; the dispatcher handles window creation, input, rendering and teardown. Use `HostedUiWindow.Post(...)` to change its session from another thread and `Close()`/App stop for shutdown. Do not operate on its UI tree or GPU context directly from a worker thread. Hosted window completion follows session/native cleanup.

External-host sessions remain on the caller's owner/render thread. Dispose them there before stopping App from another thread. Sessions own their tree and rendering resources; external GPU devices and the host frame loop remain application-owned. Dispose raster snapshots explicitly. Vulkan texture leases must be returned with their actual final image layout before resize/disposal; GPU queue access is sequential.

Migration: Alloy runtime types remain in `TrueMoon.Alloy`; Silk convenience adapters and standalone presentation live in `TrueMoon.Alloy.Hosting`. The old UI prototype moved to `TrueMoon.Alloy.Hosting.Compatibility` without type forwarders. Rebuild consumers after updating references/imports; binary compatibility across alpha changes is not promised.

Known limit: the current tested NVIDIA ICD retains GDI resources across repeated Vulkan window creation. A successful package smoke does not close K7, prove physical DPI/device-loss behavior, or establish full alpha acceptance. Linux/macOS, ARM64, single-file, trimming, NativeAOT and self-contained deployment are not covered by this package smoke.
