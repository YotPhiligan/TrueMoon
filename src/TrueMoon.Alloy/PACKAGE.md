# TrueMoon.Alloy

Retained UI sessions, bounded dispatch, frame scheduling and rendering contracts
for Argentis element trees. Requires .NET 10. Alpha API.

```xml
<PackageReference Include="TrueMoon.Alloy" Version="YOUR_VERSION" />
```

Alloy depends on `TrueMoon.Argentis` and provider-neutral `TrueMoon.Contracts`.
`IUiSessionFactory` implements `IFactory<UiSession, UiSessionCreation>` while
preserving the existing Create overload. Factory-created sessions retain explicit
caller/registry ownership; borrowed aliases do not add disposal owners.
It does not select a graphics backend or
create a native window. Choose `TrueMoon.Alloy.Rendering.Skia` for rendering,
`TrueMoon.Alloy.Platform.Silk` for the supported native window integration, or
`TrueMoon.Alloy.Hosting` to compose these with a TrueMoon App.

Sessions, tree mutation, surface operations and cleanup retain owner-thread and
frame-lease requirements. Posted work is bounded; scheduling/diagnostics do not
promise a particular GPU frame rate or release hardware acceptance. External
graphics resources remain owned by the application that supplied them.

Source, API contracts and current limits:
[TrueMoon repository](https://github.com/YotPhiligan/TrueMoon).
