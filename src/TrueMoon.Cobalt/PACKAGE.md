# TrueMoon.Cobalt

Generated dependency resolution and service lifetime management for .NET 10.
Reference `TrueMoon.Cobalt` and the separate analyzer package
`TrueMoon.Cobalt.Generator` at matching versions:

```xml
<PackageReference Include="TrueMoon.Cobalt" Version="YOUR_VERSION" />
<PackageReference Include="TrueMoon.Cobalt.Generator" Version="YOUR_VERSION" PrivateAssets="all" />
```

The runtime package does not automatically attach the analyzer to a consumer
compilation. Missing generation for a type registration is a resolution error;
adding only a ProjectReference analyzer in the framework's own build does not
generate an application's registrations.

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

Alpha API. Scope support and arbitrary constructor/declaration shapes are not
promised. See [source and module plans](https://github.com/YotPhiligan/TrueMoon).
