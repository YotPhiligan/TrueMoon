# TrueMoon.Core

Shared pooled-memory ownership, buffer writers, task scheduling and application
utilities for TrueMoon. Requires .NET 10. Alpha API.

```xml
<PackageReference Include="TrueMoon.Core" Version="YOUR_VERSION" />
```

This library is independent of the App bootstrap, DI provider, UI and graphics.
Use `TrueMoon` for application startup/lifecycle and `TrueMoon.Contracts` for
shared service interfaces. Keep ownership of pooled buffers explicit; disposal
and asynchronous work must follow the contract of the utility being used.

ProcessOutputCapture continuously drains a borrowed TextReader without waiting
for newlines. It retains a bounded live tail and can write a byte-limited UTF8
file through a separate bounded queue. Read() reports truncation and file errors;
IO failure or file overflow does not stop pipe drainage. The process owner must
await Completion after exit before releasing the reader; the utility never
disposes that borrowed reader.

The package contains its managed assembly, XML API documentation, license and
icon. Framework-wide lifecycle, IPC and UI support are supplied by their own
modules; installing Core does not enable those integrations.

Source, module status and limitations:
[TrueMoon repository](https://github.com/YotPhiligan/TrueMoon).
