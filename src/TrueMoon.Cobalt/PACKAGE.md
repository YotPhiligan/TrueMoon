# TrueMoon.Cobalt

Generated dependency resolution and service lifetime management for TrueMoon
applications on .NET 10. One package includes the runtime and its source generator:

```xml
<PackageReference Include="TrueMoon.Cobalt" Version="YOUR_VERSION" />
```

The generator is attached automatically for direct and transitive package use.
It requires the .NET 10 SDK compiler and stays out of runtime output. The two
source projects remain separate. Remove the old explicit
`TrueMoon.Cobalt.Generator` PackageReference when upgrading: that project no longer
produces a standalone package. If an older generator package is still referenced,
the bundled matching analyzer is selected once at compile time.

Set `<EnableCobaltGenerator>false</EnableCobaltGenerator>` in a consumer project
to opt out (for example, when using only runtime factories or Microsoft DI).
Type registrations needing generated construction code still require generation.
Source ProjectReference consumers retain their explicit analyzer ProjectReference;
NuGet delivery is independent of the source build graph.

```csharp
using TrueMoon.Cobalt;
using TrueMoon.Dependencies;

var resolver = new CobaltServiceResolverBuilder().Build(null!,
    [(_, services) => services.Composite<Shared, IFirst, ISecond>()]);
using ((IDisposable)resolver)
{
    var first = resolver.Resolve<IFirst>();
    var second = resolver.Resolve<ISecond>();
    Console.WriteLine(ReferenceEquals(first, second)); // True
}

public interface IFirst;
public interface ISecond;
public sealed class Shared : IFirst, ISecond;
```

Generated singleton/transient, Composite aliases and open/closed generic
dependencies have a bounded package consumer. Runtime factories are explicit;
their values are owned by the resolver. Instance registrations are borrowed.
Owned objects are disposed once by reference identity in reverse acquisition
order; cleanup continues after failures and preserves errors. These semantics
are specific to Cobalt; the Microsoft DI adapter preserves its provider's rules.

`TrySingleton<T>()`, `TrySingleton<TService, TImplementation>()` and the matching
`TryTransient` helpers are available through `using TrueMoon.Services;` and are
recognized by the analyzer in extension and static call form. The one-type helper
skips registration when the service already exists; the two-type helper checks
the exact service/implementation pair. The latter can add another implementation
for an already registered service. Duplicate registrations are preserved. Within
one service key, single-service resolution uses the last registration.
`Remove<TService,TImplementation>()` removes the first exact known pair across
lifetimes; `RemoveAll<T>()` removes every registration for that exact service key.
Neither operation disposes values from the registration context.

Registration calls support ordinary/fluent access, conditional access (`?.`),
concrete contexts and inherited contract methods. Type overloads bind arguments by
parameter, including `implementation: typeof(Concrete), service: typeof(IService)`
and open generic definitions. Static Try calls use the extension class name or a
type alias, such as `ServicesRegistrationContextExtensions.TrySingleton<Service>(context)`.
`using static` does not make extension methods available as unqualified functions
in C#; use the extension syntax or qualify the static call.

Registration modules can be ordinary methods/extensions containing concrete calls
to this API. A module library needs one Cobalt PackageReference; its generated module
initializer publishes construction code when the assembly loads. The application
calls the library's registration method during builder setup. There is no module
discovery or automatic runtime registration. Repeating a module repeats its ordinary
registrations; use Try helpers where repetition should be suppressed.

The analyzer sees supported calls in every branch, including conditional access:
it validates construction even if the branch will not execute. Generated declarations
do not add services to the runtime graph; only executed registrations do. Unrelated
methods with the same names are ignored. Registration Type arguments must be direct
`typeof` expressions (parentheses and casts to Type are supported); variables,
reflection and arbitrary generic wrapper specialization are not inferred. A generic
module using `Singleton<T>()` with an unspecified T receives COB001. Use concrete
calls in the module assembly, or explicit runtime factories/instances for dynamic
construction. Runtime Type mappings need a matching generated or manually authored
resolver; generation does not add a reflection activation fallback.

Type registrations normalize self-registration to the same service/implementation
pair. Instance registration records the actual runtime implementation type; factory
implementations stay opaque and are never discovered by executing the factory.
The two-type Try helper therefore skips a known matching instance/type registration,
but can add a type registration after an opaque factory. The one-type helper skips
any registration for the exact service key. Exist/Try inspect registered keys;
an open generic key does not pretend that every closed key is explicitly registered.

For a closed generic service, an exact closed registration takes precedence over
open registrations for single resolution. `IEnumerable<T>` merges matching closed
and open registrations in the original builder registration order, preserving each
registration's lifetime. A generic constraint failure is reported, without silently
filtering that registration. An explicit closed or open `IEnumerable<T>` registration
overrides automatic synthesis; exact closed still takes precedence. Handcrafted
containers without builder order metadata use open-before-closed merge order.

Runtime Type overloads reject null arguments and invalid class/interface mappings
before adding a registration. Both types must be fully closed or open definitions;
open mappings follow the positional rules below. Constructor code must still be
available from generation or an authored resolver; runtime validation adds no dynamic
activation fallback. Resolution requires closed types; Exist can inspect open keys.

`GetHandles()` returns a read-only collection of detached mutable metadata copies.
Changing a returned handle or the registration context cannot change a previously
built resolver. Snapshot instances and factory delegates keep their original references;
this is metadata isolation, not cloning service objects. Individual snapshot/query
operations synchronize access; composite registration and Try check/add sequences
are intended for setup and do not promise atomic execution across threads.

Construction uses the unique public instance constructor with the most parameters,
regardless of which dependencies have been registered. A missing required dependency
fails resolution; Cobalt does not fall back to a shorter constructor. Missing nullable
parameters receive null/default, and explicit parameter defaults are used when no
service value is available. Registered values take precedence, including zero and
false. Closed and open generic construction follow the same rules.

The implementation must be an accessible, non-abstract class. Internal types are
supported; private nested and file-local types are not. Constructor parameters must
be managed values: ref/in/out, pointers and ref-like types are unsupported. Required
members need a selected constructor marked `SetsRequiredMembers`, or a runtime factory.
Open service/implementation types must have equal arity and map type parameters in
the same order. Open types inside generic containing types are unsupported;
open types inside non-generic containers and closed nested types are supported.
Generic constraints are enforced when a requested type is closed at runtime.

The generator reports errors at the registration call: COB001 unsupported/inaccessible
type, COB002 no public constructor, COB003 equally long constructors, COB004 unsupported
parameter, COB005 incompatible mapping, COB006 required members. An explicit runtime
factory controls construction and bypasses these generator checks. Choose one longest
public constructor or use a factory when construction needs another rule.

Resolution errors include a dependency path. Missing required services throw
`ServiceResolvingException<T>`; cycles throw `ServiceResolvingException` with
`Failure == ServiceResolutionFailure.CircularDependency`. `ServiceType` identifies
the failing type. `GetService`/`TryResolve` return null/default for missing values,
but propagate failures from registered factories and constructors.

Factory and constructor exceptions keep their original type, object and throw site,
including user-thrown `TargetInvocationException`. Read their immutable dependency
snapshot with `ServiceResolvingException.GetDependencyPath(error)` or the
`TrueMoon.Cobalt.DependencyPath` entry in `Exception.Data`. Metadata is best effort
for custom exceptions with unavailable/read-only Data. Reusing an exception updates
its current snapshot; an earlier captured snapshot remains unchanged.

The guard identifies the active resolver by reference and closed service type,
so separate registrations and different generic arguments can depend on each other.
Resolution supports paths up to 128 entries, including synthesized enumerable
elements. Insufficient thread stack is reported as `InsufficientExecutionStack`;
the available thread stack can impose a lower practical limit. Expanding generic recursion
is bounded by these guards. Construction uses the synchronous resolver lock;
blocking a factory on another thread that resolves from the same container is unsupported.

Failed construction does not cache a singleton. Null factory results remain optional
absence or a required-service error and can retry until a value is created. Completed
dependencies from failed construction remain owned and reusable, then are disposed
at resolver shutdown in reverse acquisition order. Resources allocated inside a
constructor/factory before it throws remain that code's responsibility.

Alpha API. Scope support is not promised. See
[source and module plans](https://github.com/YotPhiligan/TrueMoon).
