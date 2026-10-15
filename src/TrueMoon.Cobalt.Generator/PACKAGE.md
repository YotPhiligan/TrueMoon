# TrueMoon.Cobalt.Generator: internal build component

C# source generator for Cobalt service registrations. Requires the .NET 10 SDK
compiler; the analyzer itself targets net10.0. Other compiler hosts/SDK versions
have not been verified.

This source project is built separately and is not packable. Its DLL is bundled
at `analyzers/dotnet/cs/TrueMoon.Cobalt.Generator.dll` in `TrueMoon.Cobalt`.
Consumers reference `TrueMoon.Cobalt` or `TrueMoon`; generation attaches
automatically through the bundled package. It stays out of runtime output.
Source builds still use an analyzer ProjectReference to this project.
See [the consumer instructions](../TrueMoon.Cobalt/PACKAGE.md).

Calls to `IServicesRegistrationContext.Singleton`, `Transient`, `Composite` and the Try helpers
produce construction code and a module initializer registering factories.
Explicit runtime factory overloads remain runtime-owned and do not generate
construction code. Constructor dependencies resolve through the selected graph;
Composite aliases share the concrete singleton. Open generics retain the
runtime reflection path rather than claiming AOT support.

To inspect output, enable `EmitCompilerGeneratedFiles` and set
`CompilerGeneratedFilesOutputPath` under `obj`. The independent package consumer
checks the actual compiler analyzer path, generated sources, runtime graph and a
negative build with `EnableCobaltGenerator=false`.

Alpha API; arbitrary declarations, other IDE compiler hosts, trimming and
NativeAOT are not verified. [Source](https://github.com/YotPhiligan/TrueMoon).
