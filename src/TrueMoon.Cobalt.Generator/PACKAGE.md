# TrueMoon.Cobalt.Generator

C# source generator for Cobalt service registrations. Requires the .NET 10 SDK
compiler; the analyzer itself targets net10.0. Other compiler hosts/SDK versions
have not been verified.

```xml
<PackageReference Include="TrueMoon.Cobalt.Generator" Version="YOUR_VERSION" PrivateAssets="all" />
```

Use the same version as `TrueMoon.Cobalt` (or `TrueMoon`). This package contains
`analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll`; it is a build-time dependency,
not a runtime assembly. Runtime packages do not automatically add it to consumer
compilations. Explicitly reference it in each application/library declaring
Cobalt registrations.

Calls to `IServicesRegistrationContext.Singleton`, `Transient` and `Composite`
produce construction code and a module initializer registering factories.
Explicit runtime factory overloads remain runtime-owned and do not generate
construction code. Constructor dependencies resolve through the selected graph;
Composite aliases share the concrete singleton. Open generics retain the
runtime reflection path rather than claiming AOT support.

To inspect output, enable `EmitCompilerGeneratedFiles` and set
`CompilerGeneratedFilesOutputPath` under `obj`. The independent package consumer
checks the actual compiler analyzer path, generated sources, runtime graph and a
negative build with the generator omitted.

Alpha API; arbitrary declarations, other IDE compiler hosts, trimming and
NativeAOT are not verified. [Source](https://github.com/YotPhiligan/TrueMoon).
