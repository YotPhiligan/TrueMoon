# TrueMoon.Enerit

Alpha typed IPC runtime for .NET10. Use the same package version and the same
service/payload sources on both peers. The named-pipe path has a bounded Windows
x64 proof; MMF, other OS, cross-version schemas, backpressure and uncooperative
shutdown remain unverified. No automatic retry/replay of failed calls.

Reference TrueMoon, TrueMoon.Enerit and TrueMoon.Enerit.Generator explicitly;
the generator reference must use PrivateAssets="all". Add TrueMoon.Mithril for
App/AddUnit owned children. The default Cobalt provider also needs an explicit
TrueMoon.Cobalt.Generator reference. Microsoft DI uses the
TrueMoon.Extensions.DependencyInjection adapter and does not require that analyzer.
Generators require the .NET10 SDK compiler and belong at build time.

Configure UseDiagnostics(), PipesServiceTransport() or
PipesServiceTransport(serviceType => endpoint), then UseInvocationService<T>() on
the caller and ListenInvocationService<T,TImplementation>() on the server.
Both peers must agree on the endpoint without the tm_ prefix. With Cobalt, explicitly
register the local implementation before Listen, so its constructor graph is visible.
App-owned transports participate in Start/Stop and failed-start rollback.

Supported codecs include native scalar values, nullable values/strings, byte Memory,
arrays/lists/dictionaries and supported mutable acyclic DTOs. Unsupported shapes give
TME001. Task/void calls wait for an empty acknowledgement; errors become
InvocationException. Response payloads must be consumed exactly. Cancellation is
local and does not send a cancel frame. Reader reconnect serves later calls only.

Failed generated decode returns nested byte rentals. Successful response rentals
belong to the caller; validated request rentals belong to the service from entry,
including when it throws. Return each whole rental with TrueMoon.MemoryPoolUtils.TryReturn
after use. Manual unscoped readers/custom callbacks must manage ownership/validation.

The source repository's ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1 builds
a local feed and isolated PackageReference consumer, checks analyzer provenance and
runs typed calls/restart/rollback/cleanup. Packages are not published by that script.
