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

Tree mutation and resource ownership follow the UI owner thread. Rich-text
shaping/painting is provided by the rendering adapter; the control model alone
does not supply IME, accessibility or complete Unicode/emoji platform support.

Source, supported scope and ownership documentation:
[TrueMoon repository](https://github.com/YotPhiligan/TrueMoon).
