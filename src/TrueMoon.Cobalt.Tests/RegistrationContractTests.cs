using System.Collections.Frozen;
using TrueMoon.Services;

namespace TrueMoon.Cobalt.Tests;

public class RegistrationContractTests
{
    private static CobaltServiceResolver Build(Action<IServicesRegistrationContext> register) =>
        (CobaltServiceResolver)new CobaltServiceResolverBuilder().Build(null!, [(_, context) => register(context)]);

    public static IEnumerable<object?[]> InvalidMappings()
    {
        yield return [null, typeof(First), "service"];
        yield return [typeof(IMarker), null, "implementation"];
        yield return [typeof(int), typeof(First), "service"];
        yield return [typeof(IMarker[]), typeof(First), "service"];
        yield return [typeof(IMarker), typeof(int), "implementation"];
        yield return [typeof(IMarker), typeof(AbstractMarker), "implementation"];
        yield return [typeof(IMarker), typeof(IMarker), "implementation"];
        yield return [typeof(IMarker), typeof(Unrelated), "implementation"];
        yield return [typeof(IBox<>), typeof(Box<int>), "implementation"];
        yield return [typeof(IBox<int>), typeof(Box<>), "implementation"];
        yield return [typeof(IBox<>), typeof(WrongArity<,>), "implementation"];
        yield return [typeof(IPair<,>), typeof(Reversed<,>), "implementation"];
        yield return [typeof(void), typeof(First), "service"];
        yield return [typeof(Action), typeof(First), "service"];
        yield return [typeof(IMarker).MakeByRefType(), typeof(First), "service"];
        yield return [typeof(int).MakePointerType(), typeof(First), "service"];
        yield return [typeof(IMarker), typeof(First[]), "implementation"];
        yield return [typeof(IMarker), typeof(Action), "implementation"];
        yield return [typeof(Box<>).GetGenericArguments()[0], typeof(First), "service"];
        yield return [typeof(IBox<>).MakeGenericType(typeof(Box<>).GetGenericArguments()[0]), typeof(Box<>), "service"];
        yield return [typeof(IBox<>), typeof(Box<>).MakeGenericType(typeof(IBox<>).GetGenericArguments()[0]), "implementation"];
        yield return [typeof(Outer<>.IItem<>), typeof(Outer<>.Item<>), "implementation"];
        yield return [typeof(IMarker), typeof(StaticClass), "implementation"];
        yield return [typeof(Converted), typeof(Converting), "implementation"];
    }

    [Theory]
    [MemberData(nameof(InvalidMappings))]
    public void TypeOverloads_RejectInvalidMappingsBeforeMutatingContext(Type? service, Type? implementation, string parameter)
    {
        foreach (var transient in new[] { false, true }) {
            var context = new CobaltServicesRegistrationContext();
            var existing = new First();
            context.Instance<IMarker>(existing);
            var error = Assert.ThrowsAny<ArgumentException>(() => {
                if (transient) context.Transient(service!, implementation!); else context.Singleton(service!, implementation!);
            });
            Assert.Equal(parameter, error.ParamName);
            if (service == null || implementation == null) Assert.IsType<ArgumentNullException>(error);
            else Assert.IsType<ArgumentException>(error);
            var handle = Assert.Single(context.GetContainer().GetHandles());
            Assert.Equal(typeof(IMarker), handle.ServiceType);
            Assert.Same(existing, handle.Instance);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidMappings))]
    public void ContainerTypeOverloads_RejectInvalidMappingsWithExactParameter(Type? service, Type? implementation, string parameter)
    {
        foreach (var transient in new[] { false, true }) {
            var container = new ServicesRegistrationContainer();
            var error = Assert.ThrowsAny<ArgumentException>(() => {
                if (transient) container.RegisterTransient(service!, implementation!); else container.RegisterSingleton(service!, implementation!);
            });
            Assert.Equal(parameter, error.ParamName);
            Assert.Empty(container.GetHandles());
        }
    }

    [Theory]
    [MemberData(nameof(InvalidMappings))]
    public void HandleTypeFactories_ValidateMappingsAndRetainOptionalSelfFallback(Type? service, Type? implementation, string parameter)
    {
        foreach (var transient in new[] { false, true }) {
            var error = Assert.ThrowsAny<ArgumentException>(() => {
                if (transient) ServiceRegistration.Transient(service!, implementation); else ServiceRegistration.Singleton(service!, implementation);
            });
            Assert.Equal(parameter == "service" ? "serviceType" : "implementationType", error.ParamName);
        }
    }

    [Fact]
    public void SelfContainerAndHandleFactories_NormalizeTypesAndRejectInvalidSelfTypes()
    {
        var singleton = ServiceRegistration.Singleton(typeof(First));
        var transient = ServiceRegistration.Transient(typeof(First), null);
        Assert.Equal(typeof(First), singleton.ImplementationType);
        Assert.Equal(typeof(First), transient.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, singleton.Lifetime);
        Assert.Equal(ServiceLifetime.Transient, transient.Lifetime);
        var container = new ServicesRegistrationContainer();
        container.RegisterSingleton<First>(); container.RegisterTransient<First>();
        Assert.All(container.GetHandles(), handle => Assert.Equal(typeof(First), handle.ImplementationType));
        Assert.Equal("service", Assert.Throws<ArgumentNullException>(() => container.RegisterSingleton((Type)null!)).ParamName);
        Assert.Equal("service", Assert.Throws<ArgumentNullException>(() => container.RegisterTransient((Type)null!)).ParamName);
        Assert.Equal("implementation", Assert.Throws<ArgumentException>(() => container.RegisterSingleton(typeof(IMarker))).ParamName);
        Assert.Equal("implementation", Assert.Throws<ArgumentException>(() => container.RegisterTransient(typeof(IMarker))).ParamName);
        Assert.Equal(2, container.GetHandles().Count);
    }

    [Fact]
    public void ValidMappings_AcceptBaseVarianceOpenInheritanceAndClosedNestedTypes()
    {
        var container = new ServicesRegistrationContainer();
        var mappings = new[] {
            (typeof(IMarker), typeof(First)), (typeof(AbstractMarker), typeof(DerivedMarker)),
            (typeof(ICovariant<object>), typeof(Covariant<string>)), (typeof(GenericBase<>), typeof(DerivedGeneric<>)),
            (typeof(Box<>), typeof(Box<>)), (typeof(Outer<int>.IItem<string>), typeof(Outer<int>.Item<string>))
        };
        foreach (var pair in mappings) { container.RegisterSingleton(pair.Item1, pair.Item2); container.RegisterTransient(pair.Item1, pair.Item2); }
        Assert.Equal(12, container.GetHandles().Count);
        for (var index = 0; index < mappings.Length; index++) {
            Assert.Equal(mappings[index].Item1, container.GetHandles()[index * 2].ServiceType);
            Assert.Equal(mappings[index].Item2, container.GetHandles()[index * 2].ImplementationType);
        }
    }

    [Fact]
    public void NullInstanceFactoryAndContainerInputs_AreRejectedWithoutAddingRegistration()
    {
        var context = new CobaltServicesRegistrationContext();
        Assert.Equal("instance", Assert.Throws<ArgumentNullException>(() => context.Instance<IMarker>(null!)).ParamName);
        Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => context.Singleton<IMarker>((Func<IServiceResolver, IMarker>)null!)).ParamName);
        Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => context.Transient<IMarker>((Func<IServiceResolver, IMarker>)null!)).ParamName);
        Assert.Equal("container", Assert.Throws<ArgumentNullException>(() => ServiceRegistration.Singleton<IMarker>((IFactoryContainer<IMarker>)null!)).ParamName);
        Assert.Equal("container", Assert.Throws<ArgumentNullException>(() => ServiceRegistration.Transient<IMarker>((IFactoryContainer<IMarker>)null!)).ParamName);
        Assert.Equal("instance", Assert.Throws<ArgumentNullException>(() => ServiceRegistration.Instance<IMarker>(null!)).ParamName);
        Assert.Empty(context.GetContainer().GetHandles());
    }

    [Fact]
    public void Snapshot_PreservesFactoriesAndBorrowedObjectIdentity_WithoutExposingLiveHandles()
    {
        var context = new CobaltServicesRegistrationContext();
        var state = new State { Value = 7 };
        context.Instance(state).Singleton<IMarker>(_ => new First());
        var first = context.GetContainer().GetHandles();
        var second = context.GetContainer().GetHandles();
        Assert.NotSame(first, second);
        Assert.Same(state, first[0].Instance);
        Assert.Same(first[1].Resolver, second[1].Resolver);
        Assert.Null(first[1].ImplementationType);
        state.Value = 11;
        Assert.Equal(11, Assert.IsType<State>(first[0].Instance).Value);
        first[1].Resolver = null;
        Assert.NotNull(context.GetContainer().GetHandles()[1].Resolver);
        context.RemoveAll<IMarker>().RemoveAll<State>();
        Assert.Empty(context.GetContainer().GetHandles());
        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
    }

    [Fact]
    public void BuiltGraph_IsolatedFromLaterContextAndSnapshotMutation()
    {
        CobaltServicesRegistrationContext source = null!;
        using var resolver = Build(c => { source = (CobaltServicesRegistrationContext)c; c.Singleton<IMarker, First>(); });
        var snapshot = source.GetContainer().GetHandles();
        snapshot[0].ServiceType = typeof(Unrelated); snapshot[0].Resolver = () => throw new Exception("metadata was used");
        source.RemoveAll<IMarker>().Singleton<IMarker, Second>();
        var value = Assert.IsType<First>(resolver.Resolve<IMarker>());
        Assert.Same(value, Assert.Single(resolver.Resolve<IEnumerable<IMarker>>()));
        Assert.True(source.Exist<IMarker, Second>());
        Assert.False(source.Exist<IMarker, First>());
    }

    [Fact]
    public async Task ConcurrentSnapshotReads_AreStableAndOrderedDuringRegistration()
    {
        var context = new CobaltServicesRegistrationContext();
        using var start = new Barrier(2);
        var snapshots = new List<IReadOnlyList<ServiceRegistrationHandle>>();
        var writer = Task.Run(() => {
            for (var id = 1; id <= 200; id++) {
                context.Instance<IMarker>(new First { Id = id });
                Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
            }
        });
        var reader = Task.Run(() => {
            for (var read = 1; read <= 200; read++) {
                Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                var snapshot = context.GetContainer().GetHandles(); snapshots.Add(snapshot);
                Assert.Equal(read, snapshot.Count);
                Assert.True(context.Exist<IMarker, First>());
                Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
            }
        });
        await Task.WhenAll(writer, reader).WaitAsync(TimeSpan.FromSeconds(10));
        for (var index = 0; index < snapshots.Count; index++)
            Assert.Equal(Enumerable.Range(1, index + 1), snapshots[index].Select(handle => Assert.IsType<First>(handle.Instance).Id));
        Assert.Equal(200, context.GetContainer().GetHandles().Count);
    }

    [Fact]
    public void RegistrationAccessor_ReturnsReadOnlyDetachedMetadataSnapshots()
    {
        var context = new CobaltServicesRegistrationContext();
        var borrowed = new First();
        context.Instance<IMarker>(borrowed);
        var original = context.GetContainer().GetHandles();
        var copy = Assert.Single(original);
        Assert.Throws<NotSupportedException>(() => ((IList<ServiceRegistrationHandle>)original).Clear());
        copy.ServiceType = typeof(Unrelated); copy.ImplementationType = typeof(Unrelated);
        copy.Lifetime = ServiceLifetime.Transient; copy.Instance = new Unrelated(); copy.Resolver = null; copy.Key = "changed";
        context.Singleton<Second>();
        var current = context.GetContainer().GetHandles();
        Assert.Single(original);
        Assert.Equal(2, current.Count);
        Assert.Equal(typeof(IMarker), current[0].ServiceType);
        Assert.Equal(typeof(First), current[0].ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, current[0].Lifetime);
        Assert.Same(borrowed, current[0].Instance);
        Assert.NotNull(current[0].Resolver);
        Assert.Null(current[0].Key);
        Assert.NotSame(copy, current[0]);
    }

    [Fact]
    public void SelfAndInstanceRegistrations_HaveExactImplementationQueriesAndRemoval()
    {
        var context = new CobaltServicesRegistrationContext();
        context.Singleton<First>();
        Assert.True(context.Exist<First, First>());
        context.Remove<First, First>();
        Assert.False(context.Exist<First>());
        context.Instance<IMarker>(new First());
        Assert.True(context.Exist<IMarker, First>());
        context.Remove<IMarker, First>();
        Assert.False(context.Exist<IMarker>());
    }

    [Fact]
    public void TwoTypeTryAfterSelfRegistration_DoesNotCreateDuplicate()
    {
        using var resolver = Build(c => c.Singleton<First>().TryTransient<First, First>());
        var values = resolver.Resolve<IEnumerable<First>>().ToArray();
        Assert.Single(values);
        Assert.Same(values[0], resolver.Resolve<First>());
        Assert.Same(values[0], resolver.Resolve<First>());
    }

    [Fact]
    public void DuplicateRegistrations_PreserveOrderLifetimeAndLastSingleSelection()
    {
        using var resolver = Build(c => c.Singleton<IMarker, First>().Transient<IMarker, First>()
            .Singleton<IMarker, Second>().Singleton<IMarker, First>());
        var first = resolver.Resolve<IEnumerable<IMarker>>().ToArray();
        var second = resolver.Resolve<IEnumerable<IMarker>>().ToArray();
        Assert.Collection(first, item => Assert.IsType<First>(item), item => Assert.IsType<First>(item), item => Assert.IsType<Second>(item), item => Assert.IsType<First>(item));
        Assert.Same(first[0], second[0]); Assert.NotSame(first[1], second[1]);
        Assert.Same(first[2], second[2]); Assert.Same(first[3], second[3]);
        Assert.NotSame(first[0], first[3]);
        Assert.Same(first[3], resolver.Resolve<IMarker>());
    }

    [Fact]
    public void RemovePair_RemovesFirstExactMatchAcrossLifetimes_AndRemoveAllIsExact()
    {
        var context = new CobaltServicesRegistrationContext();
        context.Singleton<IMarker, First>().Transient<IMarker, First>().Singleton<IMarker, Second>().Singleton<Unrelated>();
        context.Remove<IMarker, First>();
        var handles = context.GetContainer().GetHandles();
        Assert.Equal(new[] { typeof(First), typeof(Second), typeof(Unrelated) }, handles.Select(h => h.ImplementationType));
        Assert.Equal(ServiceLifetime.Transient, handles[0].Lifetime);
        context.Remove<IMarker, First>();
        Assert.False(context.Exist<IMarker, First>());
        Assert.True(context.Exist<IMarker, Second>());
        context.Remove<IMarker, First>();
        Assert.Equal(2, context.GetContainer().GetHandles().Count);
        context.RemoveAll<IMarker>();
        Assert.Equal(typeof(Unrelated), Assert.Single(context.GetContainer().GetHandles()).ServiceType);
    }

    [Fact]
    public void TryHelpers_UseExactKnownPairs_AndTreatFactoriesAsOpaque()
    {
        var borrowed = new First();
        using var resolver = Build(c => c.Instance<IMarker>(borrowed).TrySingleton<IMarker, First>()
            .TryTransient<IMarker, Second>().TrySingleton<IMarker, Second>());
        var items = resolver.Resolve<IEnumerable<IMarker>>().ToArray();
        Assert.Equal(2, items.Length); Assert.Same(borrowed, items[0]); Assert.IsType<Second>(items[1]);
        Assert.IsType<Second>(resolver.Resolve<IMarker>());
        var factoryValue = new First();
        using var opaque = Build(c => c.Singleton<IMarker>(_ => factoryValue).TrySingleton<IMarker, First>());
        var factoryItems = opaque.Resolve<IEnumerable<IMarker>>().ToArray();
        Assert.Equal(2, factoryItems.Length); Assert.Same(factoryValue, factoryItems[0]);
        Assert.IsType<First>(factoryItems[1]); Assert.NotSame(factoryValue, factoryItems[1]);
    }

    [Fact]
    public void OneTypeTry_PreservesExistingFactory_AndReaddsAfterRemoveAll()
    {
        var existing = new First();
        using var kept = Build(c => c.Singleton<First>(_ => existing).TryTransient<First>().TrySingleton<First>());
        Assert.Same(existing, Assert.Single(kept.Resolve<IEnumerable<First>>()));
        using var replaced = Build(c => c.Singleton<First>(_ => existing).RemoveAll<First>().TryTransient<First>());
        Assert.Single(replaced.Resolve<IEnumerable<First>>());
        Assert.NotSame(existing, replaced.Resolve<First>());
        Assert.NotSame(replaced.Resolve<First>(), replaced.Resolve<First>());
    }

    [Fact]
    public void ExactQueries_IncludeOpenKeysButDoNotPretendClosedRegistrationsExist()
    {
        var context = new CobaltServicesRegistrationContext();
        Type service = typeof(IBox<>); Type implementation = typeof(Box<>);
        context.Singleton(service, implementation);
        Assert.True(context.Exist(service)); Assert.True(context.Exist(service, implementation));
        Assert.False(context.Exist<IBox<int>>()); Assert.False(context.Exist(typeof(IBox<int>), typeof(Box<int>)));
        Assert.Equal("serviceType", Assert.Throws<ArgumentNullException>(() => context.Exist((Type)null!)).ParamName);
        Assert.Equal("serviceType", Assert.Throws<ArgumentNullException>(() => context.Exist(null!, implementation)).ParamName);
        Assert.Equal("implementationType", Assert.Throws<ArgumentNullException>(() => context.Exist(service, null!)).ParamName);
    }

    [Fact]
    public void ValueInstancesAndExplicitValueFactories_RemainSupported()
    {
        using var resolver = Build(c => c.Instance<int>(0).Instance<bool>(false));
        Assert.Equal(0, resolver.Resolve<int>()); Assert.False(resolver.Resolve<bool>());
        Assert.Equal(new[] { 0 }, resolver.Resolve<IEnumerable<int>>());
        var container = new ServicesRegistrationContainer();
        var calls = 0;
        container.RegisterSingleton<int>(_ => { calls++; return 0; });
        var factory = Assert.Single(container.GetHandles()).Resolver!;
        var typed = Assert.IsAssignableFrom<IResolver<int>>(factory());
        Assert.Equal(0, typed.Resolve(resolver)); Assert.Equal(0, typed.Resolve(resolver));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void OpaqueFactoryQueriesAndPairRemoval_DoNotInvokeOrGuessImplementation()
    {
        var context = new CobaltServicesRegistrationContext();
        var calls = 0;
        context.Singleton<IMarker>(_ => { calls++; return new First(); });
        Assert.True(context.Exist<IMarker>());
        Assert.False(context.Exist<IMarker, First>());
        Assert.False(context.Exist(typeof(IMarker), typeof(IMarker)));
        context.Remove<IMarker, First>();
        Assert.NotNull(Assert.Single(context.GetContainer().GetHandles()).Resolver);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void MixedGenericEnumeration_IncludesOpenAndClosedInGlobalRegistrationOrder()
    {
        using var resolver = Build(c => c.Singleton(typeof(IBox<>), typeof(Box<>))
            .Singleton<IBox<int>, IntBox>()
            .Transient(typeof(IBox<>), typeof(OtherBox<>)));
        var first = resolver.Resolve<IEnumerable<IBox<int>>>().ToArray();
        var second = resolver.Resolve<IEnumerable<IBox<int>>>().ToArray();
        Assert.Collection(first, item => Assert.IsType<Box<int>>(item), item => Assert.IsType<IntBox>(item), item => Assert.IsType<OtherBox<int>>(item));
        Assert.Same(first[0], second[0]);
        Assert.Same(first[1], second[1]);
        Assert.NotSame(first[2], second[2]);
        Assert.Same(first[1], resolver.Resolve<IBox<int>>());
        Assert.IsType<OtherBox<string>>(resolver.Resolve<IBox<string>>());
    }

    [Fact]
    public void MixedGenericEnumeration_InterleavesKeysAndDuplicates_AndRetainsClosedPrecedence()
    {
        using var resolver = Build(c => c.Singleton<IBox<int>, IntBox>().Singleton<Unrelated>()
            .Singleton(typeof(IBox<>), typeof(Box<>)).Transient<IBox<int>, IntBox>()
            .Transient(typeof(IBox<>), typeof(OtherBox<>)));
        var first = resolver.Resolve<IEnumerable<IBox<int>>>().ToArray();
        var second = resolver.Resolve<IEnumerable<IBox<int>>>().ToArray();
        Assert.Collection(first, x => Assert.IsType<IntBox>(x), x => Assert.IsType<Box<int>>(x), x => Assert.IsType<IntBox>(x), x => Assert.IsType<OtherBox<int>>(x));
        Assert.Same(first[0], second[0]); Assert.Same(first[1], second[1]);
        Assert.NotSame(first[2], second[2]); Assert.NotSame(first[3], second[3]);
        Assert.IsType<IntBox>(resolver.Resolve<IBox<int>>());
        Assert.NotSame(resolver.Resolve<IBox<int>>(), resolver.Resolve<IBox<int>>());
    }

    [Fact]
    public void RemoveClosedGeneric_LeavesOpenRegistrationAvailable()
    {
        using var resolver = Build(c => c.Singleton(typeof(IBox<>), typeof(Box<>))
            .Singleton<IBox<int>, IntBox>().RemoveAll<IBox<int>>());
        var item = Assert.IsType<Box<int>>(Assert.Single(resolver.Resolve<IEnumerable<IBox<int>>>()));
        Assert.Same(item, resolver.Resolve<IBox<int>>());
        Assert.NotSame(item, resolver.Resolve<IBox<string>>());
        Assert.Empty(resolver.Resolve<IEnumerable<IMarker>>());
    }

    [Fact]
    public void ExplicitOpenEnumerableRegistration_OverridesSynthesis_AndClosedOverridesOpen()
    {
        var closed = new IMarker[] { new First() };
        using var resolver = Build(c => c.Singleton(typeof(IEnumerable<>), typeof(ExplicitEnumerable<>))
            .Instance<IEnumerable<IMarker>>(closed).Singleton<Second>());
        Assert.Same(closed, resolver.Resolve<IEnumerable<IMarker>>());
        Assert.IsType<ExplicitEnumerable<Second>>(resolver.Resolve<IEnumerable<Second>>());
    }

    [Fact]
    public void OpenConstraintFailureDuringMixedEnumeration_IsNotSilentlyFiltered()
    {
        using var resolver = Build(c => c.Singleton<IBox<int>, IntBox>()
            .Singleton(typeof(IBox<>), typeof(ReferenceBox<>)));
        var error = Assert.Throws<ArgumentException>(() => resolver.Resolve<IEnumerable<IBox<int>>>());
        Assert.Equal(new[] { typeof(IEnumerable<IBox<int>>), typeof(IBox<int>) }, ServiceResolvingException.GetDependencyPath(error));
        Assert.IsType<IntBox>(resolver.Resolve<IBox<int>>());
        Assert.IsType<ReferenceBox<string>>(Assert.Single(resolver.Resolve<IEnumerable<IBox<string>>>()));
    }

    [Fact]
    public void OpenResolutionType_IsRejectedClearlyInsteadOfEnteringSynthesis()
    {
        using var resolver = Build(_ => {});
        Assert.Equal("type", Assert.Throws<ArgumentException>(() => resolver.GetService(typeof(IEnumerable<>))).ParamName);
        Assert.Equal("type", Assert.Throws<ArgumentException>(() => resolver.GetService(typeof(IBox<>))).ParamName);
        Assert.Equal("type", Assert.Throws<ArgumentNullException>(() => resolver.GetService(null!)).ParamName);
        Assert.Null(resolver.TryResolve<IMarker>());
    }

    [Fact]
    public void HandcraftedMixedContainers_HaveDeterministicOpenThenClosedFallbackOrder()
    {
        var open = new TypeContainer(typeof(IBox<>));
        open.Add(() => new ManualGenericResolver());
        var closed = new TypeContainer(typeof(IBox<int>));
        var instance = new IntBox();
        closed.Add(() => new InstanceResolver<IBox<int>>(instance));
        using var resolver = new CobaltServiceResolver(new[] { closed, open }.ToFrozenDictionary(c => c.Type, c => (ITypeContainer)c));
        Assert.Collection(resolver.Resolve<IEnumerable<IBox<int>>>(), x => Assert.IsType<Box<int>>(x), x => Assert.Same(instance, x));
        Assert.Same(instance, resolver.Resolve<IBox<int>>());
    }

    public interface IMarker;
    public class First : IMarker { public int Id { get; init; } }
    public class Second : IMarker;
    public abstract class AbstractMarker : IMarker;
    public class Unrelated;
    public interface IBox<T>;
    public class Box<T> : IBox<T>;
    public class OtherBox<T> : IBox<T>;
    public class IntBox : IBox<int>;
    public class WrongArity<T, U> : IBox<T>;
    public interface IPair<T, U>;
    public class Reversed<T, U> : IPair<U, T>;
    public static class StaticClass;
    public class Converted;
    public class Converting { public static implicit operator Converted(Converting value) => new(); }
    public class Outer<T> { public interface IItem<U>; public class Item<U> : IItem<U>; }
    public class DerivedMarker : AbstractMarker;
    public interface ICovariant<out T>;
    public class Covariant<T> : ICovariant<T>;
    public abstract class GenericBase<T>;
    public class DerivedGeneric<T> : GenericBase<T>;
    public class State { public int Value { get; set; } }
    public class ReferenceBox<T> : IBox<T> where T : class;
    public class ExplicitEnumerable<T> : IEnumerable<T> {
        public IEnumerator<T> GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private class ManualGenericResolver : IGenericResolver {
        public bool IsServiceDisposable => false;
        public ServiceLifetime ServiceLifetime => ServiceLifetime.Transient;
        public object? Resolve(Type[] arguments, IServiceResolver context) => Activator.CreateInstance(typeof(Box<>).MakeGenericType(arguments));
    }
}
