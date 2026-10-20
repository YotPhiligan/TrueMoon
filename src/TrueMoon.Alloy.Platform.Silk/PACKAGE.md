# TrueMoon.Alloy.Platform.Silk

Silk.NET window/input and OpenGL/Vulkan presentation integration for Alloy.
The currently verified native scope is Windows x64 with .NET 10. Alpha API.

```xml
<PackageReference Include="TrueMoon.Alloy.Platform.Silk" Version="YOUR_VERSION" />
```

This package supplies the platform adapter and depends on Alloy/Silk.NET. It
does not choose a renderer, register application services or provide a complete
UI application. `TrueMoon.Alloy.Hosting` composes native windows, compatible
rendering sessions and App lifetime when that integration is needed.

Native window, input and presentation resources require their owning thread
and explicit cleanup. Renderer/target/presenter capabilities must agree before
window creation. A successful package build does not prove physical DPI,
device-loss or long-running driver/resource acceptance.

Source, platform requirements and known hardware limitations:
[TrueMoon repository](https://github.com/YotPhiligan/TrueMoon).
