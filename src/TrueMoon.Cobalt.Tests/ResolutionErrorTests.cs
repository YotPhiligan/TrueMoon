using System.Collections;
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Reflection;
using TrueMoon.Services;

namespace TrueMoon.Cobalt.Tests;

public class ResolutionErrorTests
{
    private static CobaltServiceResolver Build(Action<IServicesRegistrationContext> register) =>
        (CobaltServiceResolver)new CobaltServiceResolverBuilder().Build(null!, [(_, context) => register(context)]);

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)]
    public void DirectFactoryCycle_FailsBeforeRecursion_ThroughEveryEntryPoint(bool transient, int entry)
    {
        var calls = 0;
        Func<IServiceResolver, Marker> factory = r => {
            if (++calls > 8) throw new InvalidOperationException("Bounded test recursion guard.");
            return entry switch { 0 => r.Resolve<Marker>(), 1 => (Marker)r.GetService(typeof(Marker))!, _ => r.TryResolve<Marker>()! };
        };
        using var resolver = Build(c => { if (transient) c.Transient(factory); else c.Singleton(factory); });
        var error = Assert.Throws<ServiceResolvingException>(() => resolver.Resolve<Marker>());
        Assert.Equal(1, calls);
        Assert.Equal(ServiceResolutionFailure.CircularDependency, error.Failure);
        Assert.Equal(typeof(Marker), error.ServiceType);
        Assert.Contains("Circular", error.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { typeof(Marker), typeof(Marker) },
            Assert.IsAssignableFrom<IReadOnlyList<Type>>(error.Data["TrueMoon.Cobalt.DependencyPath"]));
    }

    [Fact]
    public void IndirectFactoryCycle_ReportsFullDependencyPath_AndDoesNotPoisonHealthyService()
    {
        var calls = 0;
        using var resolver = Build(c => c.Singleton<Marker>(r => {
            if (++calls > 8) throw new InvalidOperationException("Bounded test recursion guard.");
            r.Resolve<Other>(); return new Marker();
        }).Transient<Other>(r => { r.Resolve<Marker>(); return new Other(); }).Singleton<Healthy>());
        var error = Assert.Throws<ServiceResolvingException>(() => resolver.Resolve<Marker>());
        Assert.Equal(new[] { typeof(Marker), typeof(Other), typeof(Marker) },
            Assert.IsAssignableFrom<IReadOnlyList<Type>>(error.Data["TrueMoon.Cobalt.DependencyPath"]));
        Assert.IsType<Healthy>(resolver.Resolve<Healthy>());
    }

    [Fact]
    public void MissingGeneratedDependency_ReportsParentAndRequiredType()
    {
        using var resolver = Build(c => c.Singleton<NeedsMissing>());
        var error = Assert.Throws<ServiceResolvingException<Missing>>(() => resolver.Resolve<NeedsMissing>());
        Assert.Equal(new[] { typeof(NeedsMissing), typeof(Missing) },
            Assert.IsAssignableFrom<IReadOnlyList<Type>>(error.Data["TrueMoon.Cobalt.DependencyPath"]));
        Assert.Contains(nameof(NeedsMissing), error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Missing), error.Message, StringComparison.Ordinal);
        Assert.Null(resolver.GetService(typeof(Missing)));
        Assert.Null(resolver.TryResolve<Missing>());
    }

    [Fact]
    public void OpenGenericConstructorFailure_PreservesOriginalExceptionAndStack()
    {
        var state = new ErrorState();
        using var resolver = Build(c => c.Instance(state).Singleton(typeof(Throws<>), typeof(Throws<>)));
        var error = Assert.Throws<InvalidOperationException>(() => resolver.Resolve<Throws<int>>());
        Assert.Same(state.Failure, error);
        Assert.Contains(nameof(ErrorState.Fail), error.StackTrace, StringComparison.Ordinal);
        Assert.Equal(new[] { typeof(Throws<int>) },
            Assert.IsAssignableFrom<IReadOnlyList<Type>>(error.Data["TrueMoon.Cobalt.DependencyPath"]));
    }

    [Fact]
    public void FailedSingletonFactory_RetriesAndKeepsCompletedDependenciesOwned()
    {
        var state = new ErrorState();
        var dependency = new Owned();
        var borrowed = new Borrowed();
        var attempts = 0;
        var resolver = Build(c => c.Instance(borrowed).Singleton<Owned>(_ => dependency).Singleton<Marker>(r => {
            r.Resolve<Owned>(); r.Resolve<Borrowed>();
            if (++attempts == 1) state.Fail();
            return new Marker();
        }));
        try {
            var error = Assert.Throws<InvalidOperationException>(() => resolver.Resolve<Marker>());
            Assert.Same(state.Failure, error);
            Assert.Equal(new[] { typeof(Marker) },
                Assert.IsAssignableFrom<IReadOnlyList<Type>>(error.Data["TrueMoon.Cobalt.DependencyPath"]));
            Assert.Equal(0, dependency.Disposals);
            var recovered = resolver.Resolve<Marker>();
            Assert.Same(recovered, resolver.Resolve<Marker>());
            Assert.Equal(2, attempts);
            Assert.Same(dependency, resolver.Resolve<Owned>());
        } finally { resolver.Dispose(); }
        Assert.Equal(1, dependency.Disposals);
        Assert.Equal(0, borrowed.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompositeCycle_ReportsAliasesAndEnumerableElements(bool enumerable)
    {
        using var resolver = Build(c => c.Composite<CyclicComposite, IAlias, IOtherAlias>());
        var error = Assert.Throws<ServiceResolvingException>(() => {
            if (enumerable) resolver.Resolve<IEnumerable<IAlias>>(); else resolver.Resolve<IAlias>();
        });
        var expected = new[] { typeof(IAlias), typeof(CyclicComposite), typeof(IAlias) };
        if (enumerable) expected = new[] { typeof(IEnumerable<IAlias>) }.Concat(expected).ToArray();
        AssertPath(error, expected);
        Assert.Equal(ServiceResolutionFailure.CircularDependency, error.Failure);
        Assert.Equal(typeof(IAlias), error.ServiceType);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GeneratedConstructorCycle_IsDetectedForClosedAndOpenTypes(bool open)
    {
        using var resolver = Build(c => {
            if (open) c.Singleton(typeof(Cyclic<>), typeof(Cyclic<>)); else c.Singleton<Cyclic<int>>();
        });
        var error = Assert.Throws<ServiceResolvingException>(() => resolver.Resolve<Cyclic<int>>());
        AssertPath(error, typeof(Cyclic<int>), typeof(Cyclic<int>));
        Assert.Equal(ServiceResolutionFailure.CircularDependency, error.Failure);
    }

    [Fact]
    public void CyclicFactory_CanRetryAfterFailure_WithoutLosingOwnership()
    {
        var cycle = true;
        var attempts = 0;
        var dependency = new Owned();
        var resolver = Build(c => c.Singleton<Owned>(_ => dependency).Singleton<Marker>(r => {
            attempts++;
            r.Resolve<Owned>();
            if (cycle) return r.Resolve<Marker>();
            return new Marker();
        }));
        try {
            var error = Assert.Throws<ServiceResolvingException>(() => resolver.Resolve<Marker>());
            var path = AssertPath(error, typeof(Marker), typeof(Marker));
            Assert.Throws<NotSupportedException>(() => ((IList<Type>)path)[0] = typeof(Other));
            cycle = false;
            var recovered = resolver.Resolve<Marker>();
            Assert.Same(recovered, resolver.Resolve<Marker>());
            Assert.Equal(2, attempts);
            Assert.Equal(0, dependency.Disposals);
            Assert.Equal(new[] { typeof(Marker), typeof(Marker) }, path);
        } finally { resolver.Dispose(); }
        Assert.Equal(1, dependency.Disposals);
    }

    [Fact]
    public void DifferentRegistrationsOfSameService_CanResolveOneAnother()
    {
        var shared = new Marker();
        var firstCalls = 0; var lastCalls = 0;
        using var resolver = Build(c => c.Singleton<Marker>(r => { firstCalls++; return r.Resolve<Marker>(); })
            .Singleton<Marker>(_ => { lastCalls++; return shared; }));
        var values = resolver.Resolve<IEnumerable<Marker>>().ToArray();
        Assert.Equal(2, values.Length);
        Assert.All(values, item => Assert.Same(shared, item));
        Assert.Same(shared, resolver.Resolve<Marker>());
        Assert.Equal(1, firstCalls);
        Assert.Equal(1, lastCalls);
    }

    [Fact]
    public void OpenGenericResolver_CanConstructAnotherClosedType()
    {
        using var resolver = Build(c => c.Singleton(typeof(Bridge<>), typeof(Bridge<>)));
        var root = resolver.Resolve<Bridge<int>>();
        Assert.Same(resolver.Resolve<Bridge<string>>(), root.Dependency);
        Assert.Same(root, resolver.Resolve<Bridge<int>>());
        Assert.Null(resolver.Resolve<Bridge<string>>().Dependency);
    }

    [Fact]
    public void IndependentResolvers_DoNotShareActivationState()
    {
        using var inner = Build(c => c.Singleton<Marker>());
        using var outer = Build(c => c.Singleton<Marker>(_ => inner.Resolve<Marker>()));
        Assert.Same(inner.Resolve<Marker>(), outer.Resolve<Marker>());
        Assert.Same(outer.Resolve<Marker>(), outer.Resolve<Marker>());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MissingOpenDependency_AndNullFactory_HaveStructuredErrors(bool nullFactory)
    {
        using var resolver = Build(c => {
            if (nullFactory) c.Singleton<Marker>(_ => null!);
            else c.Singleton(typeof(NeedsMissing<>), typeof(NeedsMissing<>));
        });
        var error = Assert.ThrowsAny<ServiceResolvingException>(() => {
            if (nullFactory) resolver.Resolve<Marker>(); else resolver.Resolve<NeedsMissing<int>>();
        });
        var expected = nullFactory ? new[] { typeof(Marker) } : new[] { typeof(NeedsMissing<int>), typeof(Missing) };
        AssertPath(error, expected);
        Assert.Equal(expected[^1], error.ServiceType);
        Assert.Equal(ServiceResolutionFailure.MissingService, error.Failure);
        Assert.Null(resolver.TryResolve<Missing>());
    }

    [Theory]
    [InlineData(127, false)] [InlineData(128, false)] [InlineData(129, true)]
    public void ResolutionDepth_BoundaryAndExpandingGenericGraph_AreBoundedAndRecoverable(int limit, bool failure)
    {
        RunOnThread(() => VerifyDepth(limit, failure), 8 * 1024 * 1024);
    }

    private static void VerifyDepth(int limit, bool failure)
    {
        Assert.Equal(128, CobaltServiceResolverBase.MaximumResolutionDepth);
        var progressive = new ProgressiveResolver { Limit = limit };
        var container = new TypeContainer(typeof(GenericNode<>));
        container.Add(() => progressive);
        using var resolver = new CobaltServiceResolver(new[] { container }.ToFrozenDictionary(c => c.Type, c => (ITypeContainer)c));
        if (!failure) {
            Assert.IsType<GenericNode<int>>(resolver.Resolve<GenericNode<int>>());
            Assert.Equal(limit, progressive.Calls);
        } else {
            var error = Assert.Throws<ServiceResolvingException>(() => resolver.Resolve<GenericNode<int>>());
            var path = ServiceResolvingException.GetDependencyPath(error);
            Assert.Equal(ServiceResolutionFailure.DepthExceeded, error.Failure);
            Assert.Equal(129, path.Count);
            Assert.Equal(typeof(GenericNode<int>), path[0]);
            Assert.Equal(path[^1], error.ServiceType);
            Assert.Equal(128, progressive.Calls);
            for (var index = 1; index < path.Count; index++)
                Assert.Equal(typeof(List<>).MakeGenericType(path[index - 1].GenericTypeArguments[0]), path[index].GenericTypeArguments[0]);
            progressive.Limit = 1; progressive.Calls = 0;
            Assert.IsType<GenericNode<int>>(resolver.Resolve<GenericNode<int>>());
            Assert.Equal(1, progressive.Calls);
            Assert.Equal(129, path.Count);
        }
    }

    [Fact]
    public void SmallExecutionStack_FailsObservablyBeforeOverflow_AndRemainsRecoverable()
    {
        RunOnThread(() => {
            var progressive = new ProgressiveResolver { Limit = CobaltServiceResolverBase.MaximumResolutionDepth + 1 };
            var container = new TypeContainer(typeof(GenericNode<>));
            container.Add(() => progressive);
            using var resolver = new CobaltServiceResolver(new[] { container }.ToFrozenDictionary(c => c.Type, c => (ITypeContainer)c));
            CallWithReducedStack(() => {
                var error = Assert.Throws<ServiceResolvingException>(() => resolver.Resolve<GenericNode<int>>());
                Assert.Equal(ServiceResolutionFailure.InsufficientExecutionStack, error.Failure);
                AssertPath(error, typeof(GenericNode<int>));
                Assert.Equal(0, progressive.Calls);
            });
            progressive.Calls = 0; progressive.Limit = 1;
            Assert.IsType<GenericNode<int>>(resolver.Resolve<GenericNode<int>>());
            Assert.Equal(1, progressive.Calls);
        }, 256 * 1024);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void CallWithReducedStack(Action action)
    {
        Span<byte> reserve = stackalloc byte[4096];
        reserve[0] = 1;
        if (System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack()) CallWithReducedStack(action);
        else action();
        Assert.Equal((byte)1, reserve[0]);
    }

    private static void RunOnThread(Action action, int stackSize)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => {
            try { action(); completed.TrySetResult(); }
            catch (Exception exception) { completed.TrySetException(exception); }
        }, stackSize) { IsBackground = true };
        thread.Start();
        completed.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Owned depth test thread did not exit.");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MetadataFailure_DoesNotReplaceCustomException(bool inaccessible)
    {
        var original = new MetadataException(inaccessible);
        using var resolver = Build(c => c.Singleton<Marker>(_ => throw original));
        Assert.Same(original, Assert.Throws<MetadataException>(() => resolver.Resolve<Marker>()));
        Assert.Empty(ServiceResolvingException.GetDependencyPath(original));
    }

    [Fact]
    public void ExceptionPath_IsImmutable_AndUpdatedWhenOriginalExceptionIsReused()
    {
        var original = new InvalidOperationException("reused exception");
        using var resolver = Build(c => c.Singleton<Marker>(_ => throw original)
            .Singleton<Other>(r => { r.Resolve<Marker>(); return new Other(); }));
        Assert.Same(original, Assert.Throws<InvalidOperationException>(() => resolver.Resolve<Other>()));
        var first = AssertPath(original, typeof(Other), typeof(Marker));
        Assert.Same(original, Assert.Throws<InvalidOperationException>(() => resolver.Resolve<Marker>()));
        var second = AssertPath(original, typeof(Marker));
        Assert.Equal(new[] { typeof(Other), typeof(Marker) }, first);
        Assert.NotSame(first, second);
        Assert.Empty(ServiceResolvingException.GetDependencyPath(new Exception()));
        Assert.Throws<ArgumentNullException>(() => ServiceResolvingException.GetDependencyPath(null!));
    }

    [Fact]
    public void ReusedMissingException_UpdatesTheCurrentPathWithoutMutatingPreviousSnapshot()
    {
        var original = new ServiceResolvingException(typeof(Missing));
        Assert.Equal(typeof(Missing), original.ServiceType);
        Assert.Equal(ServiceResolutionFailure.MissingService, original.Failure);
        AssertPath(original, typeof(Missing));
        using var resolver = Build(c => c.Singleton<Marker>(_ => throw original)
            .Singleton<Other>(r => { r.Resolve<Marker>(); return new Other(); }));
        Assert.Same(original, Assert.Throws<ServiceResolvingException>(() => resolver.Resolve<Other>()));
        var previous = AssertPath(original, typeof(Other), typeof(Marker), typeof(Missing));
        Assert.Same(original, Assert.Throws<ServiceResolvingException>(() => resolver.Resolve<Marker>()));
        AssertPath(original, typeof(Marker), typeof(Missing));
        Assert.Equal(new[] { typeof(Other), typeof(Marker), typeof(Missing) }, previous);
        Assert.Throws<ArgumentNullException>(() => new ServiceResolvingException(null!));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void NullSingletonFactoryResult_IsOptionalOrRequired_AndCanRetryToAValue(int entry)
    {
        var calls = 0;
        var value = new Marker();
        using var resolver = Build(c => c.Singleton<Marker>(_ => ++calls == 1 ? null! : value));
        switch (entry) {
            case 0:
                var error = Assert.Throws<ServiceResolvingException<Marker>>(() => resolver.Resolve<Marker>());
                AssertPath(error, typeof(Marker));
                Assert.Equal(ServiceResolutionFailure.MissingService, error.Failure);
                break;
            case 1: Assert.Null(resolver.GetService(typeof(Marker))); break;
            case 2: Assert.Null(resolver.TryResolve<Marker>()); break;
        }
        Assert.Same(value, resolver.Resolve<Marker>());
        Assert.Same(value, resolver.Resolve<Marker>());
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ResolverIdentity_IsReferenceBasedEvenWhenCustomResolversCompareEqual()
    {
        var value = new Marker();
        var container = new TypeContainer(typeof(Marker));
        var first = new EqualResolver(r => r.Resolve<Marker>());
        var last = new EqualResolver(_ => value);
        Assert.True(first.Equals(last));
        container.Add(() => first); container.Add(() => last);
        using var resolver = new CobaltServiceResolver(new[] { container }.ToFrozenDictionary(c => c.Type, c => (ITypeContainer)c));
        var items = resolver.Resolve<IEnumerable<Marker>>().ToArray();
        Assert.Equal(2, items.Length);
        Assert.All(items, item => Assert.Same(value, item));
    }

    [Fact]
    public void GenericConstraintFailure_IdentifiesClosedService_AndAllowsAnotherTypeArgument()
    {
        using var resolver = Build(c => c.Singleton(typeof(IConstrained<>), typeof(Constrained<>)));
        var error = Assert.Throws<ArgumentException>(() => resolver.Resolve<IConstrained<int>>());
        AssertPath(error, typeof(IConstrained<int>));
        Assert.Contains(nameof(Constrained<object>), error.Message, StringComparison.Ordinal);
        var valid = resolver.Resolve<IConstrained<string>>();
        Assert.IsType<Constrained<string>>(valid);
        Assert.Same(valid, resolver.Resolve<IConstrained<string>>());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PartialEnumerableFailure_RetainsCompletedValuesForRetryOrShutdown(bool retry)
    {
        var order = new List<string>();
        var first = new Element("first", order);
        var second = new Element("second", order);
        var original = new InvalidOperationException("enumerable element failure");
        var attempts = 0;
        var resolver = Build(c => c.Singleton<IElement>(_ => first).Singleton<IElement>(_ => {
            if (++attempts == 1) throw original; return second;
        }));
        try {
            Assert.Same(original, Assert.Throws<InvalidOperationException>(() => resolver.Resolve<IEnumerable<IElement>>()));
            AssertPath(original, typeof(IEnumerable<IElement>), typeof(IElement));
            Assert.Equal(0, first.Disposals);
            Assert.Equal(0, second.Disposals);
            if (retry) {
                var items = resolver.Resolve<IEnumerable<IElement>>().ToArray();
                Assert.Equal(new IElement[] { first, second }, items);
                Assert.Same(second, resolver.Resolve<IElement>());
                Assert.Equal(2, attempts);
            } else Assert.Equal(1, attempts);
        } finally { resolver.Dispose(); }
        Assert.Equal(retry ? new[] { "second", "first" } : new[] { "first" }, order);
        Assert.Equal(1, first.Disposals);
        Assert.Equal(retry ? 1 : 0, second.Disposals);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void UserTargetInvocationException_IsPreserved_ForTypedClosedAndOpenPaths(int route)
    {
        var original = new TargetInvocationException(new InvalidOperationException("user-owned inner"));
        var state = new FailureState(original);
        CobaltServiceResolver resolver;
        Type expected;
        if (route == 0) {
            var container = new TypeContainer(typeof(Marker));
            container.Add(() => new TypedFailureResolver(original));
            resolver = new CobaltServiceResolver(new[] { container }.ToFrozenDictionary(c => c.Type, c => (ITypeContainer)c));
            expected = typeof(Marker);
        } else {
            resolver = Build(c => {
                c.Instance(state);
                if (route == 1) c.Singleton<UserFailure<int>>(); else c.Singleton(typeof(UserFailure<>), typeof(UserFailure<>));
            });
            expected = typeof(UserFailure<int>);
        }
        using (resolver) {
            var error = Assert.Throws<TargetInvocationException>(() => resolver.GetService(expected));
            Assert.Same(original, error);
            Assert.Same(original.InnerException, error.InnerException);
            AssertPath(error, expected);
        }
    }

    [Fact]
    public async Task ConcurrentSingletonFailure_LeavesOneRetryAndNoFalseCycles()
    {
        var original = new InvalidOperationException("first attempt");
        var attempts = 0;
        using var resolver = Build(c => c.Singleton<Marker>(_ => {
            if (++attempts == 1) throw original;
            return new Marker();
        }));
        var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() => {
            try { return (Value: resolver.Resolve<Marker>(), Error: (Exception?)null); }
            catch (Exception error) { return (Value: (Marker?)null, Error: error); }
        }));
        var outcomes = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Same(original, Assert.Single(outcomes, o => o.Error != null).Error);
        var successful = outcomes.Where(o => o.Error == null).ToArray();
        Assert.Equal(31, successful.Length);
        var value = resolver.Resolve<Marker>();
        Assert.All(successful, outcome => Assert.Same(value, outcome.Value));
        Assert.Equal(2, attempts);
        AssertPath(original, typeof(Marker));
    }

    [Theory]
    [InlineData(false, false, false)] [InlineData(false, false, true)]
    [InlineData(false, true, false)] [InlineData(false, true, true)]
    [InlineData(true, false, false)] [InlineData(true, false, true)]
    [InlineData(true, true, false)] [InlineData(true, true, true)]
    public async Task GeneratedConstructorFailure_RetriesAndCleansCompletedDependenciesInReverseOrder(bool open, bool transient, bool asynchronous)
    {
        var state = new RetryState();
        var borrowed = new Borrowed();
        var resolver = Build(c => {
            c.Instance(state).Instance(borrowed).Transient<RecordedDependency>(_ => {
                var dependency = new RecordedDependency(state);
                state.Dependencies.Add(dependency); return dependency;
            });
            if (open) {
                if (transient) c.Transient(typeof(RetryService<>), typeof(RetryService<>));
                else c.Singleton(typeof(RetryService<>), typeof(RetryService<>));
            } else {
                if (transient) c.Transient<RetryService<int>>(); else c.Singleton<RetryService<int>>();
            }
        });
        try {
            var error = Assert.Throws<InvalidOperationException>(() => resolver.Resolve<RetryService<int>>());
            Assert.Same(state.Failure, error);
            AssertPath(error, typeof(RetryService<int>));
            Assert.Equal(0, Assert.Single(state.Dependencies).Disposals);
            var recovered = resolver.Resolve<RetryService<int>>();
            var repeated = resolver.Resolve<RetryService<int>>();
            if (transient) Assert.NotSame(recovered, repeated); else Assert.Same(recovered, repeated);
            Assert.Equal(transient ? 3 : 2, state.Attempts);
            Assert.Equal(transient ? 3 : 2, state.Dependencies.Count);
            Assert.Empty(state.Cleanup);
        } finally {
            if (asynchronous) await resolver.DisposeAsync(); else resolver.Dispose();
        }
        Assert.Equal(transient ? new[] { "parent2", "dependency3", "parent1", "dependency2", "dependency1" } :
            new[] { "parent1", "dependency2", "dependency1" }, state.Cleanup);
        Assert.All(state.Dependencies, dependency => Assert.Equal(1, dependency.Disposals));
        Assert.Equal(0, borrowed.Disposals);
    }

    private static IReadOnlyList<Type> AssertPath(Exception error, params Type[] expected)
    {
        var path = ServiceResolvingException.GetDependencyPath(error);
        Assert.Equal(expected, path);
        return path;
    }

    public class Marker;
    public class Other;
    public class Healthy;
    public class Missing;
    public class NeedsMissing(Missing dependency) { public Missing Dependency { get; } = dependency; }
    public class NeedsMissing<T>(Missing dependency) { public Missing Dependency { get; } = dependency; }
    public class Cyclic<T>(Cyclic<T> dependency) { public Cyclic<T> Dependency { get; } = dependency; }
    public interface IAlias;
    public interface IOtherAlias;
    public class CyclicComposite(IAlias alias) : IAlias, IOtherAlias { public IAlias Alias { get; } = alias; }
    public class Bridge<T> {
        public Bridge(IServiceResolver resolver) { if (typeof(T) == typeof(int)) Dependency = resolver.Resolve<Bridge<string>>(); }
        public object? Dependency { get; }
    }
    public class GenericNode<T>;
    public interface IConstrained<T>;
    public class Constrained<T> : IConstrained<T> where T : class;
    public interface IElement;
    private class Element(string name, List<string> order) : IElement, IDisposable {
        public int Disposals { get; private set; }
        public void Dispose() { Disposals++; order.Add(name); }
    }
    private class ProgressiveResolver : IGenericResolver {
        public int Calls { get; set; }
        public int Limit { get; set; }
        public bool IsServiceDisposable => false;
        public ServiceLifetime ServiceLifetime => ServiceLifetime.Transient;
        public object? Resolve(Type[] arguments, IServiceResolver context) {
            if (++Calls > CobaltServiceResolverBase.MaximumResolutionDepth + 4) throw new InvalidOperationException("Bounded test recursion guard.");
            if (Calls < Limit) context.GetService(typeof(GenericNode<>).MakeGenericType(typeof(List<>).MakeGenericType(arguments[0])));
            return Activator.CreateInstance(typeof(GenericNode<>).MakeGenericType(arguments));
        }
    }
    private class MetadataException(bool inaccessible) : Exception {
        private readonly IDictionary _data = new ReadOnlyDictionary<object, object>(new Dictionary<object, object>());
        public override IDictionary Data => inaccessible ? throw new InvalidOperationException("Metadata unavailable.") : _data;
    }
    private class TypedFailureResolver(Exception error) : IResolver<Marker> {
        public bool IsServiceDisposable => false;
        public ServiceLifetime ServiceLifetime => ServiceLifetime.Transient;
        public Marker Resolve(IServiceResolver context) => throw error;
    }
    private class EqualResolver(Func<IServiceResolver, Marker> factory) : IResolver<Marker> {
        public bool IsServiceDisposable => false;
        public ServiceLifetime ServiceLifetime => ServiceLifetime.Transient;
        public Marker Resolve(IServiceResolver context) => factory(context);
        public override bool Equals(object? other) => other is EqualResolver;
        public override int GetHashCode() => 1;
    }
    public class FailureState(Exception error) { public void Fail() => throw error; }
    public class UserFailure<T> { public UserFailure(FailureState state) => state.Fail(); }
    public class RetryState {
        public InvalidOperationException Failure { get; } = new("constructor failure");
        public int Attempts { get; set; }
        public int Parents { get; set; }
        public List<RecordedDependency> Dependencies { get; } = [];
        public List<string> Cleanup { get; } = [];
    }
    public class RecordedDependency(RetryState state) : IDisposable {
        private readonly int _id = state.Dependencies.Count + 1;
        public int Disposals { get; private set; }
        public void Dispose() { Disposals++; state.Cleanup.Add("dependency" + _id); }
    }
    public class RetryService<T> : IDisposable {
        private readonly RetryState _state;
        private readonly int _id;
        public RetryService(RecordedDependency dependency, Borrowed borrowed, RetryState state) {
            _state = state;
            if (++state.Attempts == 1) throw state.Failure;
            _id = ++state.Parents;
        }
        public void Dispose() => _state.Cleanup.Add("parent" + _id);
    }
    public class Throws<T> { public Throws(ErrorState state) => state.Fail(); }
    public class ErrorState {
        public InvalidOperationException Failure { get; } = new("original construction failure");
        public void Fail() => throw Failure;
    }
    public class Owned : IDisposable {
        public int Disposals { get; private set; }
        public void Dispose() => Disposals++;
    }
    public class Borrowed : Owned;
}
