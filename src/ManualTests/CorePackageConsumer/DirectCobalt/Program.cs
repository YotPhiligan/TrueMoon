using System.Text.Json;
using Direct.Consumer;
using TrueMoon.Cobalt;
using TrueMoon.Dependencies;

if (args.Length != 1) return 2;
var state = new Evidence();
string? failure = null;
try
{
    var marker = typeof(RDirectCobaltConsumer.Cobalt.Generated.CobaltResolversRegistration);
    Check.Require(marker.Assembly == typeof(Shared).Assembly, "Consumer generator marker missing.");
    var resolver = new CobaltServiceResolverBuilder().Build(null!, [(_, services) => services
        .Instance(state).Instance(state.Borrowed)
        .Composite<Shared, IFirst, ISecond>()
        .Singleton(typeof(IValue<>), typeof(Value<>))
        .Transient<Transient>()]);
    using ((IDisposable)resolver)
    {
        var concrete = resolver.Resolve<Shared>()!;
        Check.Require(ReferenceEquals(concrete, resolver.Resolve<IFirst>()) && ReferenceEquals(concrete, resolver.Resolve<ISecond>()), "Composite aliases differ.");
        var number = resolver.Resolve<IValue<int>>()!;
        Check.Require(number.Name == "Int32" && ReferenceEquals(number, resolver.Resolve<IValue<int>>())
            && resolver.Resolve<IValue<string>>()!.Name == "String", "Generic resolution failed.");
        Check.Require(!ReferenceEquals(resolver.Resolve<Transient>(), resolver.Resolve<Transient>()), "Transients were shared.");
        Check.Require(ReferenceEquals(state.Borrowed, resolver.Resolve<Borrowed>()), "Borrowed instance changed.");
    }
    Check.Require(state.SharedDisposals == 1 && state.TransientDisposals == 2 && state.Borrowed.Disposals == 0, "Direct Cobalt ownership failed.");
}
catch (Exception error) { failure = error.ToString(); }
File.WriteAllText(args[0], JsonSerializer.Serialize(new { Completed = failure == null, state.SharedDisposals, state.TransientDisposals, BorrowedDisposals = state.Borrowed.Disposals, Failure = failure }));
Console.WriteLine(failure ?? "Direct Cobalt package graph: passed.");
return failure == null ? 0 : 1;
