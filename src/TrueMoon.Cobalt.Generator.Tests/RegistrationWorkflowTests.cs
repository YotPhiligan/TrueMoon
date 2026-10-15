using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;

namespace TrueMoon.Cobalt.Generator.Tests;

public class RegistrationWorkflowTests
{
    [Theory]
    [InlineData("Singleton")]
    [InlineData("Transient")]
    [InlineData("TrySingleton")]
    [InlineData("TryTransient")]
    public void ConditionalRegistrations_ActivateOnlyExecutedBranches_WithDeclaredLifetime(string method)
    {
        var assembly = Compile($$"""
            using System;
            using System.Linq;
            using TrueMoon.Services;
            public interface IService;
            public class Concrete : IService;
            public class Other;
            public class Chained;
            public static class Graph {
                public static IServiceResolver Create(bool enabled) => new TrueMoon.Cobalt.CobaltServiceResolverBuilder()
                    .Build(null!, [(_, c) => {
                        IServicesRegistrationContext? optional = enabled ? c : null;
                        optional?.{{method}}<IService, Concrete>()?.Singleton<Other>().Transient<Chained>();
                    }]);
            }
            """);
        var service = assembly.GetType("IService")!;
        foreach (var enabled in new[] { false, true })
        {
            var resolver = (TrueMoon.Services.IServiceResolver)assembly.GetType("Graph")!.GetMethod("Create")!.Invoke(null, [enabled])!;
            using var owner = (IDisposable)resolver;
            var first = resolver.GetService(service);
            if (!enabled)
            {
                Assert.Null(first);
                Assert.Null(resolver.GetService(assembly.GetType("Other")!));
                Assert.Null(resolver.GetService(assembly.GetType("Chained")!));
                Assert.Empty(((System.Collections.IEnumerable)resolver.GetService(typeof(IEnumerable<>).MakeGenericType(service))!).Cast<object>());
                continue;
            }
            Assert.Equal(assembly.GetType("Concrete"), first?.GetType());
            var second = resolver.GetService(service);
            if (method.EndsWith("Singleton")) Assert.Same(first, second);
            else Assert.NotSame(first, second);
            var other = assembly.GetType("Other")!;
            Assert.NotNull(resolver.GetService(other));
            Assert.Same(resolver.GetService(other), resolver.GetService(other));
            var chained = assembly.GetType("Chained")!;
            Assert.NotNull(resolver.GetService(chained));
            Assert.NotSame(resolver.GetService(chained), resolver.GetService(chained));
            Assert.Single(((System.Collections.IEnumerable)resolver.GetService(typeof(IEnumerable<>).MakeGenericType(service))!).Cast<object>());
            Assert.Null(resolver.GetService(assembly.GetType("Concrete")!));
        }
    }

    [Theory]
    [InlineData("TrySingleton", false)]
    [InlineData("TrySingleton", true)]
    [InlineData("TryTransient", false)]
    [InlineData("TryTransient", true)]
    public void AliasQualifiedStaticTryRegistrations_ExecuteAndSuppressDuplicates(string method, bool mapping)
    {
        var arguments = mapping ? "IService, Concrete" : "Concrete";
        var assembly = Compile($$"""
            using TrueMoon.Services;
            using Helpers = TrueMoon.Services.ServicesRegistrationContextExtensions;
            public interface IService;
            public class Concrete : IService;
            public static class Graph {
                public static IServiceResolver Create() => new TrueMoon.Cobalt.CobaltServiceResolverBuilder()
                    .Build(null!, [(_, c) => { Helpers.{{method}}<{{arguments}}>(context: c); Helpers.{{method}}<{{arguments}}>(c); }]);
            }
            """);
        var resolver = (TrueMoon.Services.IServiceResolver)assembly.GetType("Graph")!.GetMethod("Create")!.Invoke(null, null)!;
        using var owner = (IDisposable)resolver;
        var service = assembly.GetType(mapping ? "IService" : "Concrete")!;
        var first = resolver.GetService(service);
        Assert.Equal(assembly.GetType("Concrete"), first?.GetType());
        if (method == "TrySingleton") Assert.Same(first, resolver.GetService(service));
        else Assert.NotSame(first, resolver.GetService(service));
        Assert.Single(((System.Collections.IEnumerable)resolver.GetService(typeof(IEnumerable<>).MakeGenericType(service))!).Cast<object>());
    }

    [Theory]
    [InlineData("Singleton", "typeof(IService), typeof(Concrete)")]
    [InlineData("Singleton", "implementation: typeof(Concrete), service: typeof(IService)")]
    [InlineData("Singleton", "typeof(IService), implementation: typeof(Concrete)")]
    [InlineData("Singleton", "implementation: (System.Type)(typeof(Concrete)), service: (typeof(IService))")]
    [InlineData("Transient", "typeof(IService), typeof(Concrete)")]
    [InlineData("Transient", "implementation: typeof(Concrete), service: typeof(IService)")]
    [InlineData("Transient", "typeof(IService), implementation: typeof(Concrete)")]
    [InlineData("Transient", "implementation: (System.Type)(typeof(Concrete)), service: (typeof(IService))")]
    public void TypeRegistrations_BindSemanticParameters_RegardlessOfArgumentOrder(string method, string arguments)
    {
        var assembly = Compile($$"""
            using TrueMoon.Services;
            public interface IService;
            public class Concrete : IService;
            public static class Graph {
                public static IServiceResolver Create() => new TrueMoon.Cobalt.CobaltServiceResolverBuilder()
                    .Build(null!, [(_, c) => c.{{method}}({{arguments}})]);
            }
            """);
        var resolver = (TrueMoon.Services.IServiceResolver)assembly.GetType("Graph")!.GetMethod("Create")!.Invoke(null, null)!;
        using var owner = (IDisposable)resolver;
        var service = assembly.GetType("IService")!;
        var first = resolver.GetService(service);
        Assert.Equal(assembly.GetType("Concrete"), first?.GetType());
        if (method == "Singleton") Assert.Same(first, resolver.GetService(service));
        else Assert.NotSame(first, resolver.GetService(service));
        Assert.Null(resolver.GetService(assembly.GetType("Concrete")!));
    }

    [Theory]
    [InlineData("Singleton")]
    [InlineData("Transient")]
    [InlineData("Composite")]
    [InlineData("TrySingleton")]
    [InlineData("TryTransient")]
    public void UnrelatedSameNamedMethods_DoNotPublishResolversOrDiagnostics(string method)
    {
        var types = method == "Composite" ? "Abstract, Abstract, Abstract" : "Abstract";
        var result = TestHelper.Generate($$"""
            using TrueMoon.Services;
            public abstract class Abstract;
            public static class Unrelated {
                public static IServicesRegistrationContext {{method}}<T>(this string value) => null!;
                public static IServicesRegistrationContext {{method}}<T, U, V>(this string value) => null!;
            }
            public class Graph {
                public void Register() { "".{{method}}<{{types}}>(); }
            }
            """);
        Assert.Empty(result.Run.GeneratedTrees);
        Assert.Empty(result.Run.Diagnostics);
        Assert.Empty(result.Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void ConcreteAndInheritedContextCalls_ImplementContract_AndGenerateResolvers()
    {
        var assembly = Compile("""
            using TrueMoon.Services;
            using System.Collections.Frozen;
            using System.Linq;
            public class First;
            public class Second;
            public class Third;
            public class CustomContext : TrueMoon.Cobalt.CobaltServicesRegistrationContext {
                public void Register() { Singleton<First>(); base.Transient<Second>(); }
            }
            public static class Graph {
                public static IServiceResolver Create() {
                    var context = new CustomContext(); context.Register(); context.Singleton<Third>();
                    var containers = context.GetContainer().GetHandles().Select(h => {
                        var container = new TrueMoon.Cobalt.TypeContainer(h.ServiceType);
                        container.Add(TrueMoon.Cobalt.ServiceResolvers.Shared.GetResolvers()[h.ServiceType]);
                        return (TrueMoon.Cobalt.ITypeContainer)container;
                    });
                    return new TrueMoon.Cobalt.CobaltServiceResolver(containers.ToFrozenDictionary(c => c.Type));
                }
            }
            """);
        var resolver = (TrueMoon.Services.IServiceResolver)assembly.GetType("Graph")!.GetMethod("Create")!.Invoke(null, null)!;
        using var owner = (IDisposable)resolver;
        foreach (var name in new[] { "First", "Second", "Third" })
        {
            var type = assembly.GetType(name)!;
            var first = resolver.GetService(type);
            Assert.Equal(type, first?.GetType());
            if (name == "Second") Assert.NotSame(first, resolver.GetService(type));
            else Assert.Same(first, resolver.GetService(type));
        }
    }

    [Fact]
    public void ConditionalCompositeModule_PreservesAliasIdentity_AndRepeatedModuleTrySemantics()
    {
        var assembly = Compile("""
            using System;
            using System.Linq;
            using TrueMoon.Services;
            using Helpers = TrueMoon.Services.ServicesRegistrationContextExtensions;
            public interface IFirst;
            public interface ISecond;
            internal class Both : IFirst, ISecond;
            public class Dependency;
            public static class Modules {
                public static void Register(IServicesRegistrationContext? context, bool enabled) {
                    if (enabled) context?.Composite<Both, IFirst, ISecond>();
                    Helpers.TrySingleton<Dependency>(context!);
                }
            }
            public static class Graph {
                public static bool Run() {
                    using var owner = (IDisposable)new TrueMoon.Cobalt.CobaltServiceResolverBuilder()
                        .Build(null!, [(_, c) => { Modules.Register(c, false); Modules.Register(c, true); }]);
                    var r = (IServiceResolver)owner;
                    return ReferenceEquals(r.Resolve<Both>(), r.Resolve<IFirst>()) &&
                        ReferenceEquals(r.Resolve<IFirst>(), r.Resolve<ISecond>()) &&
                        r.Resolve<System.Collections.Generic.IEnumerable<Dependency>>().Count() == 1;
                }
            }
            """);
        Assert.Equal(true, assembly.GetType("Graph")!.GetMethod("Run")!.Invoke(null, null));
    }

    [Fact]
    public void ConditionalInvalidConstruction_ReportsDiagnosticAtInvocation_EvenWhenBranchIsDisabled()
    {
        var result = TestHelper.Generate("""
            using TrueMoon.Services;
            public abstract class Abstract;
            public class Graph { public void Register(IServicesRegistrationContext? context) => context?.Singleton<Abstract>(); }
            """);
        var diagnostic = Assert.Single(result.Run.Diagnostics);
        Assert.Equal("COB001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(".Singleton<Abstract>()", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        Assert.Empty(result.Run.GeneratedTrees);
    }

    [Fact]
    public void FactoriesInstancesAndRuntimeTypeVariables_DoNotInferConstruction()
    {
        var result = TestHelper.Generate("""
            using System;
            using TrueMoon.Services;
            public interface IService;
            public class Graph {
                public void Register(IServicesRegistrationContext? context, Type service, Type implementation, IService instance) {
                    context?.Singleton<IService>(_ => instance)?.Transient<IService>(_ => instance)?.Instance(instance);
                    context?.Singleton(service, implementation);
                    context?.Transient(typeof(IService), implementation);
                }
            }
            """);
        Assert.Empty(result.Run.Diagnostics);
        Assert.Empty(result.Run.GeneratedTrees);
        Assert.Empty(result.Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Theory]
    [InlineData("Singleton")]
    [InlineData("Transient")]
    public void ConditionalOpenTypeRegistration_UsesNamedMappingAndClosedLifetime(string method)
    {
        var assembly = Compile($$"""
            using TrueMoon.Services;
            public interface IBox<T>;
            internal class Box<T> : IBox<T>;
            public static class Graph {
                public static IServiceResolver Create() => new TrueMoon.Cobalt.CobaltServiceResolverBuilder()
                    .Build(null!, [(_, c) => c?.{{method}}(implementation: typeof(Box<>), service: typeof(IBox<>))]);
            }
            """);
        var resolver = (TrueMoon.Services.IServiceResolver)assembly.GetType("Graph")!.GetMethod("Create")!.Invoke(null, null)!;
        using var owner = (IDisposable)resolver;
        foreach (var argument in new[] { typeof(int), typeof(string) })
        {
            var service = assembly.GetType("IBox`1")!.MakeGenericType(argument);
            var first = resolver.GetService(service);
            Assert.Equal(assembly.GetType("Box`1")!.MakeGenericType(argument), first?.GetType());
            if (method == "Singleton") Assert.Same(first, resolver.GetService(service));
            else Assert.NotSame(first, resolver.GetService(service));
        }
    }

    [Fact]
    public void GenericRegistrationModule_RequiresConcreteTypesOrExplicitFactory()
    {
        var result = TestHelper.Generate("""
            using TrueMoon.Services;
            public static class Module {
                public static void Register<T>(IServicesRegistrationContext c) where T : class => c.Singleton<T>();
            }
            """);
        var diagnostic = Assert.Single(result.Run.Diagnostics);
        Assert.Equal("COB001", diagnostic.Id);
        Assert.Contains("factory", diagnostic.GetMessage());
        Assert.Equal("c.Singleton<T>()", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        Assert.Empty(result.Run.GeneratedTrees);

        var supported = TestHelper.Generate("""
            using System;
            using TrueMoon.Services;
            public static class Module {
                public static void Register<T>(IServicesRegistrationContext c, Func<IServiceResolver, T> factory) where T : class => c.Singleton(factory);
            }
            """);
        Assert.Empty(supported.Run.Diagnostics);
        Assert.Empty(supported.Run.GeneratedTrees);
        Assert.Empty(supported.Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void UsingStatic_DoesNotImportExtensionsAsUnqualifiedCalls()
    {
        var result = TestHelper.Generate("""
            using TrueMoon.Services;
            using static TrueMoon.Services.ServicesRegistrationContextExtensions;
            public class Concrete;
            public static class Module {
                public static void Register(IServicesRegistrationContext c) => TrySingleton<Concrete>(c);
            }
            """);
        Assert.Empty(result.Run.Diagnostics);
        Assert.Empty(result.Run.GeneratedTrees);
        var error = Assert.Single(result.Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal("CS0103", error.Id);
    }

    private static Assembly Compile(string source)
    {
        var result = TestHelper.Generate(source);
        Assert.Empty(result.Run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        using var stream = new MemoryStream();
        var emitted = result.Output.Emit(stream);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        return Assembly.Load(stream.ToArray());
    }
}
