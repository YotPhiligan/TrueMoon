# TrueMoon.Argentis

Backend-independent retained UI controls, property bindings, layout and input
contracts for TrueMoon Alloy. Requires .NET 10. Alpha API.

```xml
<PackageReference Include="TrueMoon.Argentis" Version="YOUR_VERSION" />
```

Argentis defines element trees and controls, including the rich-text document/
editing model. It has no renderer, native window, DI or application bootstrap
dependency. A visual application also needs a compatible Alloy renderer and
host; `TrueMoon.Alloy.Hosting` provides the optional App/window integration.

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
