# TrueMoon.Argentis

Current delivery profile (CORE8, 2026-11-11): **net11.0 / .NET 11**, built with
SDK 11.0.100 and runtime-async=on. Use matching TrueMoon package versions.
Native UI acceptance is bounded to Windows x64; other OS/RID, AOT/trimming,
performance and release gates require separate evidence. Dated net10 results
below remain historical and do not define the current package target.

Themes and new controls use `FontDefaults.Family` (`Segoe UI Variable Text`) at
weight400. `FontDefaults.FallbackFamily` is `Segoe UI`; rendering providers resolve
availability. `FontWeight(600)` selects semibold, and `FontItalic` requests italic
when the family supplies it. Element/style/view-style/theme overrides retain their
usual priority. A renderer configured with an explicit file catalog continues to
use that catalog. Color-theme changes can preserve FontFamily/FontSize/FontWeight/
FontItalic while replacing the palette.

`NumberBox.AdjustmentEnabled` opts into Up/Down stepping and pointer dragging on
its prefix or geometric marker. Configure NumericKind, Step and inclusive bounds;
invalid/nonfinite/overflow values are rejected without coercion or clamping.
Integer arithmetic and finite integer bounds retain exact Int64 precision. Shift
multiplies the step by10, Control divides by10 (integer minimum1); drag snapshots
modifiers and uses4logical pixels per step. Text editing still accepts partial drafts.
CanAdjust is a borrowed admission callback; IsGestureCurrent fences an active
gesture's application identity. Release retains the draft, CancelGesture restores
its exact initial string, and external/context changes interrupt without rollback.
GestureStarted/GestureEnded report capture lifecycle; no outcome commits application
data. Capture is released before terminal callbacks, including callback failures.
Dispose clears callbacks and ends owned interaction; it never owns the data model.


`PropertyEditorRow` composes a retained label, value editor and optional accessory.
Its preferred value width can be capped or fill available space; narrow layouts
move the label above the editor. `ShowEmpty` presents an explicit absent value
without clearing/replacing the owned editor. Null/validation/commit semantics stay
with the consumer. `RichTextEditor.MinimumContentHeight` and `MaximumContentHeight`
bound automatic content measurement separately from explicit Height/MinHeight;
defaults preserve the existing available-space behavior. `CheckBox.IsReadOnly`
retains focus but suppresses toggle/Click activation. Its actual indicator uses
the shared state outline and semantic palette; OnEditorAccent colors its check.


Compact numeric authoring uses `NumberBox.Editor` (a retained TextBox holding a
string draft) and `NumberBox.Prefix`. `Vector3Editor` owns three NumberBoxes in
equal columns, moving its shared Label above them when space is limited; optional
diagnostics can be appended to FirstCell/SecondCell/ThirdCell. Neither control
parses or commits engine values. The consumer supplies validation and transactions.
`ColorRole` selects a themed editor/axis accent and is inherited by composite
parts. `IsModified` indicates a pending edit; `HasError`, focus, hover and enabled
state select themed outlines. Explicit BorderColor overrides retain their usual
priority; error/edit markers and the independent focus ring remain visible.
Theme carries dark/light Number/Text/Boolean/Reference and AxisX/Y/Z tokens plus
Modified/BorderHover. Replacing a palette or style preserves existing editor state.


Backend-independent retained UI controls, property bindings, layout and input
contracts for TrueMoon Alloy. Requires .NET 11. Alpha API.

```xml
<PackageReference Include="TrueMoon.Argentis" Version="YOUR_VERSION" />
```

Argentis defines element trees and controls, including the rich-text document/
editing model. It has no renderer, native window, DI or application bootstrap
dependency. A visual application also needs a compatible Alloy renderer and
host; `TrueMoon.Alloy.Hosting` provides the optional App/window integration.

`Window` / `Window<TData>` are standalone root components with the same content,
bindings and scoped styles as View. Construction allocates no native resources.
The typed form implements `IView<TData>` and borrows its data; it derives from
`Window`, so window factories and styles can share the non-generic root contract.

```csharp
public sealed class SettingsWindow : Window<SettingsViewModel>
{
    public SettingsWindow(SettingsViewModel data, IViewStyle<SettingsWindow> style)
    {
        this.WithTitle("Settings").WithClientSize(800, 600);
        Content(data, model => new TextBox { Value = model!.Name });
        this.WithViewStyle(style);
    }
}
```

`TitleProperty` and `ClientSizeProperty` support normal bindings/style precedence.
Requested client dimensions are whole logical pixels in [1,32768]; device/platform
limits may be narrower. Inherited Width/Height affect content layout only.
`State`, `IsActive` and `ActualClientSize` are observed host state; native resize
does not overwrite the requested size. `HostStateChanged` runs on the UI owner.
`Minimize`, `Maximize`, `Restore` and `RequestCloseAsync` require a connected host.
`Closing` callbacks may veto Application/Native close; Shutdown drains all callbacks
and ignores vetoes. Cancellation cancels a caller's wait, not an accepted close.

Use Hosting's `UsePresentationFactory<SettingsWindow>` with an injected
`IFactory<SettingsWindow>`. The window owns the returned component/content, while
data/style/controller remain borrowed. A Window must be the presentation root.
Custom hosts use `AttachHost(IWindowController)` and its exclusive
`WindowHostConnection` on the UI owner, publish observations, snapshot callbacks
for their close coordinator and disconnect before tree disposal. No automatic
Show/modal/multi-window service or native resource ownership is implied.

When importing both Argentis and Silk windowing, qualify the native static
factory as `Silk.NET.Windowing.Window.Create(...)` to distinguish the two types.

Author screens as `View<TData>` with Fluent composition and explicit bindings.
Data may be a plain model; observable presentation models derive from `ViewModel`,
which derives from `Bindable` and reuses its `INotifyPropertyChanged` implementation.
Use `Bindable` directly for observable data that is not a view model. Both classes
live in `TrueMoon.Argentis` and keep state/actions independent of controls and themes.

```csharp
using TrueMoon.Argentis;

public sealed class SettingsViewModel : ViewModel
{
    private string _name = "";
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
}
```

`SetProperty` uses the default equality comparer, returns whether the field changed,
and raises `PropertyChanged` after committing a new value. `CallerMemberName` supplies
the property name; pass it explicitly when updating from a helper or refresh method.
Use `OnPropertyChanged(nameof(ComputedProperty))` for dependent properties; null or
empty names notify all properties. Observer exceptions propagate after the update.
Notifications are synchronous on the caller's thread; models retain their existing
synchronization and lifetime policy. A disposable model can release current observers
with `ClearPropertyChangedSubscribers()` after its own cleanup. `ViewModel` adds no
automatic dependency ownership. Bindings still accept any `INotifyPropertyChanged`.

Separate appearance into `IViewStyle<TView>.Configure(IViewStyleBuilder<TView>)`.
Declare `Root()`, `Part(view => view.Header)` or `Role(sharedTypedRole)` rules, then
apply/replace/remove them with `view.WithViewStyle(style)` / `WithViewStyle(null)`.
Rules expose typed `Set(UiProperty<T>, value)` and theme-dependent values, along
with helpers for spacing, text and colors. `element.WithRole(role)` preserves its
concrete type and styles later-added controls within the same view boundary.

Precedence is local/binding, direct element Style, view style, Theme, default.
Later matching rules override earlier ones. Nested views have their own boundary,
including their root. Part selectors require live controls already in the owning
view; dynamic collections use shared `ViewStyleRole<TElement>` identity tokens.
Theme/style changes preserve the existing tree and input state; invalid values
leave the previous theme/layer in place. Builders become immutable after Configure.
Use pure theme callbacks and stateless style declarations; they must not retain
view instances or run application actions. Explicit DataContext rebinding remains
the author's responsibility. View creation/factories and model lifetimes belong
to the host; a view borrows its injected model/style and owns its controls/bindings.

Tree mutation and resource ownership follow the UI owner thread. Rich-text
shaping/painting is provided by the rendering adapter; the control model alone
does not supply IME, accessibility or complete Unicode/emoji platform support.

Source, supported scope and ownership documentation:
[TrueMoon repository](https://github.com/YotPhiligan/TrueMoon).

TextBox keeps the full measured font line height. For compact fixed heights, effective
vertical padding gives way to the line while preserving the top/bottom ratio; the
vertical clip stays inside the border rather than inside padding. Horizontal padding
and validation-badge exclusion remain in effect. Natural height still includes the
configured padding. A field physically shorter than its font remains bounded by its
outer rectangle; text is not scaled down.

Validation presentation: bind HasError and ErrorMessage on TextBox/RichTextEditor
or NumberBox. A solid Theme.Error square at the right edge spans the field height;
Theme.OnError supplies the contrasting centered symbol. Narrow fields clip the badge.
ValidationIconBounds denotes the whole colored tooltip target. Text/caret/selection
and the focus ring stop before it; rich auto-height wrapping includes its width.
NumberBox.Editor inherits parent validation after local/element/view style overrides.
The existing explicit NumberBox.Editor validation bindings also remain supported.
With an OverlayHost in the tree, hover for500ms shows the exact
message in a wrapped/scrollable tooltip without changing focus/selection. Model
validation, draft storage and commit policy stay with the consumer.

Element.Shadow accepts immutable BoxShadow(sigma,offsetX,offsetY,color); local null
disables it. MenuPopup uses Theme.MenuShadow, while semantic panel View styles can
set UiProperties.Shadow from Theme.PanelShadow. Layout-only containers stay plain.
Shadows change painting only, outside own bounds inside ancestor clips, including
subtree opacity. Legacy contexts without IShadowDrawingContext omit the effect.

## Resizable panes

`SplitView` owns two retained pane hosts and a focusable divider. Horizontal
places panes left/right; Vertical places them above/below. Nest splits for
navigation, workspace, inspector and timeline layouts.

```csharp
var split = new SplitView(navigation, workspace) { FirstPaneSize = 220 };
split.FirstPane.MinWidth = 140;
split.FirstPane.MaxWidth = 400;
split.SecondPane.MinWidth = 240;
split.Divider.AccessibleName = "Navigation width";
```

FirstPaneSize is a preferred logical size (NaN = half); bind it with
FirstPaneSizeProperty when the application owns persistence. Host resize clamps
ActualFirstPaneSize without overwriting the preference. Axis Min/Max on the pane
hosts constrain allocation; insufficient space compresses minima proportionally,
while finite maxima leave trailing space. Content must be distinct and unowned;
SetContent transfers detached old content to the caller without disposal.

Primary drag captures the divider; release completes the UI preference. Escape,
focus/capture loss, hide/disable/detach/dispose or geometry changes cancel.
External preference updates interrupt without rollback. Tab, axis arrows, Shift
for tenfold steps and Home/End work without a pointer. Theme and resize retain
children, drafts and selection; there are no timers, file IO or idle redraw.
Collapse, docking, cursor shapes and OS accessibility are outside v1. Style
Divider.Background/Foreground with local properties or a typed View style;
colors default to semantic theme states.
