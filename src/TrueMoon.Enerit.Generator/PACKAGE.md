# TrueMoon.Enerit.Generator

Current delivery profile (CORE8, 2026-11-11): **net11.0 / .NET 11**, built with
SDK 11.0.100 and runtime-async=on. Use matching TrueMoon package versions.
Native UI acceptance is bounded to Windows x64; other OS/RID, AOT/trimming,
performance and release gates require separate evidence. Dated net10 results
below remain historical and do not define the current package target.

Build-time source generator for Enerit service adapters and payload codecs.
Reference it explicitly with PrivateAssets="all", alongside the same version of
TrueMoon.Enerit. The current analyzer targets net11.0 and requires the .NET11 SDK
compiler; earlier compilers and cross-version generated/runtime combinations are
unverified. The package contains analyzers/dotnet/cs/TrueMoon.Enerit.Generator.dll,
not a runtime library. Do not ship it with the application output.

UseInvocationService<T>() and ListenInvocationService<T,TImplementation>() declarations
trigger generation of client, handler, codecs and App-local typed factories.
The generated adapters do not require Cobalt to discover generated classes.
A Cobalt consumer still explicitly registers its own local implementation and
references TrueMoon.Cobalt.Generator. Microsoft DI can construct local services
without the Cobalt analyzer.

TME001 rejects unsupported interfaces/payload shapes. Clients and handlers must use
identical service sources and this generator version; method IDs/native layouts
have no schema negotiation. Strict response validation and failed nested-buffer
cleanup require the matching updated runtime. Successful rentals belong to the
caller/service and must be returned after use.

Independent package/analyzer delivery is exercised by the source repository's
ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1. This does not establish MMF,
cross-platform, trimming/NativeAOT or compiler compatibility outside the stated scope.
