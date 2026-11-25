# TrueMoon.Alloy.Hosting

Current delivery profile (CORE8, 2026-11-11): **net11.0 / .NET 11**, built with
SDK 11.0.100 and runtime-async=on. Use matching TrueMoon package versions.
Native UI acceptance is bounded to Windows x64; other OS/RID, AOT/trimming,
performance and release gates require separate evidence. Dated net10 results
below remain historical and do not define the current package target.

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

For an ordinary view, register `.View<MyView>()` or `.View<MyView, MyStyle>()` and
call `UsePresentation<MyView>(configure)`. This resolves `IFactory<MyView>` on the
UI owner thread, not `MyView` as a service. The Argentis analyzer generates the
constructor calls. Ordinary dependencies, including interfaces, resolve by their
declared parameter type. A concrete `View` constructor parameter is created by
the registered `IFactory<TChild>` before the parent constructor runs; register
the child with `.View<TChild>()` or supply its factory explicitly. Unadopted
children are disposed if construction fails or the parent is disposed first.
Use an explicit factory parameter for lazy or repeated creation.

Both overloads apply a DI-registered `IViewStyle<MyView>`
if present; the second registers a default singleton style if not already registered.
An error resolving the style fails creation before the view constructor runs.

Use `.ViewWithArgs<RowView, Item>()` to generate `IFactory<RowView, Item>`.
Pass `rows.Create` to `BindItems`; DI does not resolve the `Item` value.
Exactly one parameter of the selected longest public constructor must match
`Item` (`ARG021` otherwise). The other parameters and an optional DI-registered
`IViewStyle<RowView>` follow the ordinary activation rules.

Without the analyzer, register `IFactory<MyView>` explicitly or use
`context.UsePresentation(resolver => new MyView(resolver.Resolve<MyModel>()), configure)`.
The window owns the returned root. Session/lifecycle facades borrow their target,
avoiding multiple provider-owned disposable aliases.
Renderer/window settings are snapshotted during registration. Return a fresh,
detached root; null/disposed/parented/attached roots fault startup without disposing
another owner's tree. Factory exceptions remain observable through Completion.

The root may be an Argentis `Window` / `Window<TData>`. Use the same
`UsePresentation<TWindow>` with an explicit `IFactory<TWindow>`.
Effective component Title/ClientSize take precedence over SilkUiWindowOptions
Title/Width/Height, including their component defaults. Other native/backend
settings remain host configuration. Root construction is now before native
allocation, allowing initial component settings and root validation to run first;
root factories must not depend on a current graphics context. Fresh roots are
disposed on failed startup; foreign parented/attached/hosted roots remain untouched.

The host adds the existing frame once, connects component actions to its own
coordinator, and polls effective properties on the owner, including style/theme
changes. Native and default painted captions stay synchronized. Observed size/state
does not rewrite requested properties or rebuild the tree. Component Closing
callbacks precede host Closing callbacks in each attempt; a component veto stops
the user attempt before host handlers run. Shutdown still drains every callback.
The host disconnects the component before disposing the UI tree and native resources.
Nested Window roots are rejected; ordinary View roots retain their existing frame
configuration and close handlers. Reusing a disposed component requires a new factory product.

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

Optional App integration for Alloy/Argentis UI: standalone OpenGL/Vulkan windows or sessions in an external host. Current native delivery supports **Windows x64 and .NET 11**. This is an alpha API; preparation of local packages does not establish release readiness.

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
using TrueMoon.Argentis.Views;
using TrueMoon.Extensions.DependencyInjection;

await App.Builder(options => options.UseDI()).RunAsync(app =>
{
    app.Services(services => services.View<MainView>());
    app.UsePresentation<MainView>(options => options
        .UseSkiaOpenGL()
        .UseSilkWindow(window => window.Title = "My Alloy application"));
});

public sealed class MainView : View
{
    public MainView() => Content(ui => ui.VStack(
        ui.Text("Hello from Alloy"),
        ui.Button("Action").OnClick(() => Console.WriteLine("Clicked"))).Padding(16));
}
```

Select `UseSkiaVulkan()` instead of `UseSkiaOpenGL()` before building the App to use Vulkan. Raster sessions use `UseAlloy(options => options.UseSkiaRaster().UseExternalHost())`, a `RasterUiTarget` and an application-owned frame loop.

Cobalt is the default provider; omit `UseDI()` to use it with the same `.View<…>()`
registration. Both providers get the Argentis analyzer automatically through the
transitive `TrueMoon.Argentis` package. No separate assembly or package reference
is needed. To opt out, set `<EnableArgentisGenerator>false</EnableArgentisGenerator>`
and use an explicit `IFactory<TView>` or root callback instead of `.View<…>()`.

For reproducible font selection, load your licensed font files with `SkiaFontCatalog.Load(defaultFamily, files)` from `TrueMoon.Alloy.Rendering.Skia` and pass the catalog to `UseSkiaRaster(fonts)`, `UseSkiaOpenGL(fonts)` or `UseSkiaVulkan(fonts)`. Deliver those files and their license as application content with `CopyToOutputDirectory` and `CopyToPublishDirectory`. Loading validates fonts through native Skia before App startup; the renderer package does not bundle demo fonts. Unregistered font families use the catalog's default face without system font lookup.

For installed fonts, `SkiaFontCatalog.FromSystemFonts(["Segoe UI Variable Text", "Segoe UI"], fileCatalog)`
configures requested-family → ordered system families → file catalog selection through
the same `UseSkia*` overloads. Omit the final file catalog to use the system default
when all names are missing. Set `Theme.FontWeight`/`Element.FontWeight` or view-style
`FontWeight(600)` for semibold; `FontItalic` selects italic. The choice is shared by
layout and drawing. Default themes/controls use Segoe UI Variable Text; parameterless
Skia backends use SystemDefault with Segoe UI fallback. File-only catalogs remain
explicit. System-font metrics depend on the machine and optical sizing is not automatic.

For an existing control outside a deferred view factory, `new TextBox().BindTwoWay(TextBox.ValueProperty, model, state => state.Name)` binds an `INotifyPropertyChanged` model to an editor. Import `TrueMoon.Argentis.Editors` for `TextBox` and `TrueMoon.Argentis` for `BindTwoWay`. Keep durable state in the model and create a fresh UI tree when recreating a session.

`new RichTextEditor(new RichTextEditorOptions { WordWrap = true, UndoLimit = 100 })`
provides multiline text, immutable `RichTextDocument`/`RichTextRun` styling,
selection, plain-text clipboard, undo/redo and read-only options. Bind
`RichTextEditor.DocumentProperty` for rich content or `TextProperty` for LF plain text.
Use `Select(start, length)` and `ApplyStyle(new RichTextStyle { Bold = true })` for
formatting; indices are UTF-16 grapheme boundaries. Topten.RichTextKit arrives
transitively through Rendering.Skia. A toolbar is ordinary application composition.

App configuration/Build do not create windows or graphics contexts. Standalone windows share a process owner thread; the dispatcher handles window creation, input, rendering and teardown. Use `HostedUiWindow.Post(...)` to change its session from another thread and `Close()`/App stop for shutdown. Do not operate on its UI tree or GPU context directly from a worker thread. Hosted window completion follows session/native cleanup.

External-host sessions remain on the caller's owner/render thread. Dispose them there before stopping App from another thread. Sessions own their tree and rendering resources; external GPU devices and the host frame loop remain application-owned. Dispose raster snapshots explicitly. Vulkan texture leases must be returned with their actual final image layout before resize/disposal; GPU queue access is sequential.

**Breaking change in `1.2611.25.15-alpha`:** rename `UsePresentationFactory<TView>`
to `UsePresentation<TView>`. The generic overload no longer resolves the view as
a service. Register `.View<TView>()` or `IFactory<TView>`, or use the explicit root callback.

**Breaking change in `1.2611.25.13-alpha`:** `TrueMoon.Alloy.Hosting.Compatibility`
and its no-options `UsePresentation<TView>()` are removed without type forwarders.
Remove the Compatibility import. Register `.View<TView>()` or `IFactory<TView>`
and call `UsePresentation<TView>(o => o.UseSkiaOpenGL().UseSilkWindow())`. The root
must be an Argentis `Element`. Use `HostedUiWindow` for the App-owned window and
register custom drawing through `ConfigurePresenters` or `UsePresenterFactory`, not `VisualTree`
or `IContentPresenter`. Use `UseAlloy` for an external host. Rebuild consumers with
matching Argentis and Alloy packages.

Known limit: the current tested NVIDIA ICD retains GDI resources across repeated Vulkan window creation. A successful package smoke does not close K7, prove physical DPI/device-loss behavior, or establish full alpha acceptance. Linux/macOS, ARM64, single-file, trimming, NativeAOT and self-contained deployment are not covered by this package smoke.

## Unified standalone window settings

Add `WindowConfigurationSchema.Default` (`Alloy:Window`) to the App profile and call
`UseConfiguredPresentation(rootFactory, options => options.UseSkiaVulkan(), configure)`.
Only scalar window/graphics/frame settings bind from sources; renderer factories,
fonts, appearance hooks and view creation stay explicit. All fields are startup-only.
Build/binding does not start a window or a GPU context. `WindowConfigurationSchema.Create`
provides application-specific defaults and pure validation, as used by Editor.
