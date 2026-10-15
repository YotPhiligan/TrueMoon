using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;

namespace TrueMoon.Cobalt.Generator.Tests;

public class ConstructorTests
{
    [Theory]
    [InlineData("public abstract class Service;", "Service", "COB001")]
    [InlineData("public abstract class Service<T>;", "Service<>", "COB001")]
    [InlineData("public interface Service;", "Service", "COB001")]
    [InlineData("public struct Service;", "Service", "COB001")]
    [InlineData("file class Service;", "Service", "COB001")]
    [InlineData("public class Service { private Service() {} }", "Service", "COB002")]
    [InlineData("public class Service<T> { private Service() {} }", "Service<>", "COB002")]
    [InlineData("public class Service { public Service(string s) {} public Service(int i) {} }", "Service", "COB003")]
    [InlineData("public class Service { public Service(int i) {} public Service(string s) {} }", "Service", "COB003")]
    [InlineData("public class Service<T> { public Service(string s) {} public Service(int i) {} }", "Service<>", "COB003")]
    [InlineData("public class Service { public Service(ref int i) {} }", "Service", "COB004")]
    [InlineData("public class Service<T> { public Service(in T i) {} }", "Service<>", "COB004")]
    [InlineData("public class Service { public Service(out int i) { i = 0; } }", "Service", "COB004")]
    [InlineData("public class Service<T> where T : allows ref struct { public Service(T item) {} }", "Service<>", "COB004")]
    [InlineData("public class Service { public Service(System.Span<int> items) {} }", "Service", "COB004")]
    [InlineData("public class Service { public required string Name { get; init; } }", "Service", "COB006")]
    [InlineData("public class Service<T> { public required T Value { get; init; } }", "Service<>", "COB006")]
    [InlineData("public class Parent { public required string Name { get; init; } } public class Service : Parent;", "Service", "COB006")]
    public void InvalidConstruction_ReportsDiagnosticAtRegistration_WithoutGeneratedCompilerErrors(
        string declaration, string type, string diagnosticId)
    {
        var registration = $"context.Singleton(typeof({type}), typeof({type}))";
        AssertDiagnostic($$"""
            using TrueMoon.Services;
            {{declaration}}
            public class Graph {
                public void Register(IServicesRegistrationContext context) => {{registration}};
            }
            """, registration, diagnosticId);
    }

    [Fact]
    public void PrivateNestedImplementation_ReportsInaccessibleTypeAtRegistration()
    {
        const string registration = "context.TryTransient<Service>()";
        AssertDiagnostic("""
            using TrueMoon.Services;
            public class Graph {
                private class Service;
                public void Register(IServicesRegistrationContext context) => context.TryTransient<Service>();
            }
            """, registration, "COB001");
    }

    [Theory]
    [InlineData("public interface IService<T>; public class Service<T, U> : IService<T>;", "IService<>", "Service<,>")]
    [InlineData("public interface IService<T>; public class Service<T> : IService<T>;", "IService<int>", "Service<>")]
    [InlineData("public interface IService<T>; public class Service<T> : IService<T>;", "IService<>", "Service<int>")]
    [InlineData("public interface IService<T, U>; public class Service<T, U> : IService<U, T>;", "IService<,>", "Service<,>")]
    [InlineData("public interface IService; public class Service;", "IService", "Service")]
    [InlineData("public class IService; public class Service { public static implicit operator IService(Service value) => new(); }", "IService", "Service")]
    [InlineData("public class Outer<T> { public class Service<U>; }", "Outer<>.Service<>", "Outer<>.Service<>")]
    public void UnsupportedMapping_ReportsDiagnosticAtRegistration(string declaration, string service, string implementation)
    {
        var registration = $"context.Transient(typeof({service}), typeof({implementation}))";
        AssertDiagnostic($$"""
            using TrueMoon.Services;
            {{declaration}}
            public class Graph {
                public void Register(IServicesRegistrationContext context) => {{registration}};
            }
            """, registration, "COB005");
    }

    [Fact]
    public void InvalidComposite_DoesNotPublishAliasesForUnconstructibleConcrete()
    {
        const string registration = "context.Composite<Service, IOne, ITwo>()";
        AssertDiagnostic("""
            using TrueMoon.Services;
            public interface IOne;
            public interface ITwo;
            public abstract class Service : IOne, ITwo;
            public class Graph {
                public void Register(IServicesRegistrationContext context) => context.Composite<Service, IOne, ITwo>();
            }
            """, registration, "COB001");
    }

    [Theory]
    [InlineData("int*")]
    [InlineData("delegate*<int>")]
    public void UnsafePointerParameters_ReportUnsupportedParameter(string parameter)
    {
        const string registration = "context.Singleton<Service>()";
        AssertDiagnostic($$"""
            using TrueMoon.Services;
            public unsafe class Service { public Service({{parameter}} value) {} }
            public class Graph {
                public void Register(IServicesRegistrationContext context) => context.Singleton<Service>();
            }
            """, registration, "COB004", allowUnsafe: true);
    }

    [Fact]
    public void RegistrationWithUnspecifiedTypeParameter_ReportsUnsupportedType()
    {
        const string registration = "context.Singleton<Service<T>>()";
        AssertDiagnostic("""
            using TrueMoon.Services;
            public class Service<T>;
            public class Graph {
                public void Register<T>(IServicesRegistrationContext context) => context.Singleton<Service<T>>();
            }
            """, registration, "COB001");
    }

    [Fact]
    public void RuntimeFactory_BypassesConstructorDiagnostics()
    {
        var result = TestHelper.Generate("""
            using TrueMoon.Services;
            public class Service {
                private Service() {}
                public static Service Create() => new();
            }
            public class Graph {
                public void Register(IServicesRegistrationContext context) => context.Singleton<Service>(_ => Service.Create());
            }
            """);
        Assert.Empty(result.Run.Diagnostics);
        Assert.Empty(result.Run.GeneratedTrees);
        Assert.Empty(result.Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Diagnostics_PreserveValidRegistrations_AndReportEveryInvalidSite()
    {
        const string source = """
            using TrueMoon.Services;
            public class Valid;
            public abstract class Invalid;
            public class Graph {
                public void Register(IServicesRegistrationContext context) {
                    context.Singleton<Invalid>();
                    context.TryTransient<Invalid>();
                    context.Singleton<Valid>();
                }
            }
            """;
        var result = TestHelper.Generate(source);
        Assert.Equal(new[] { "COB001", "COB001" }, result.Run.Diagnostics.Select(d => d.Id));
        Assert.Equal(new[] { "context.Singleton<Invalid>()", "context.TryTransient<Invalid>()" },
            result.Run.Diagnostics.Select(d => source.Substring(d.Location.SourceSpan.Start, d.Location.SourceSpan.Length)));
        Assert.Equal(2, result.Run.GeneratedTrees.Length);
        Assert.DoesNotContain(result.Run.GeneratedTrees, tree => tree.ToString().Contains("new global::Invalid"));
        Assert.Empty(result.Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstantDefaults_AndNullableValueParameters_ArePreserved(bool open)
    {
        var declaration = open ? "Service<T>" : "Service";
        var type = open ? "Service<>" : "Service";
        var resolved = open ? "Service<int>" : "Service";
        var result = TestHelper.Generate($$"""
            using System;
            using TrueMoon.Services;
            public class {{declaration}} {
                public object[] Values { get; }
                public Service(int? optional, string escaped = "line\n\"quoted\"", char marker = '\'', bool enabled = true,
                    uint unsigned = uint.MaxValue, ulong wide = ulong.MaxValue, float single = 1.5f,
                    double nan = double.NaN, double infinity = double.PositiveInfinity, decimal price = 1.25m) {
                    Values = [optional == null, escaped, marker, enabled, unsigned, wide, single,
                        double.IsNaN(nan), double.IsPositiveInfinity(infinity), price];
                }
            }
            public static class Graph {
                public static object[] Run() {
                    using var ownership = (IDisposable)new TrueMoon.Cobalt.CobaltServiceResolverBuilder().Build(null!, [(_, c) => c
                        .Singleton(typeof({{type}}), typeof({{type}}))]);
                    return ((IServiceResolver)ownership).Resolve<{{resolved}}>().Values;
                }
            }
            """);
        Assert.Equal(new object[] { true, "line\n\"quoted\"", '\'', true, uint.MaxValue, ulong.MaxValue, 1.5f, true, true, 1.25m }, Execute(result));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullableGenericParameter_UsesSymbolContract_ForValueAndReferenceArguments(bool open)
    {
        var registrations = open ? "c.Transient(typeof(Service<>), typeof(Service<>));" :
            "c.Transient<Service<int>>().Transient<Service<string>>();";
        var result = TestHelper.Generate($$"""
            using System;
            using TrueMoon.Services;
            public class Service<T>(T? value) { public T? Value { get; } = value; }
            public static class Graph {
                public static object[] Run() {
                    using var ownership = (IDisposable)new TrueMoon.Cobalt.CobaltServiceResolverBuilder().Build(null!, [(_, c) => { {{registrations}} }]);
                    var services = (IServiceResolver)ownership;
                    return [services.Resolve<Service<int>>().Value, services.Resolve<Service<string>>().Value == null];
                }
            }
            """);
        Assert.Equal(new object[] { 0, true }, Execute(result));
    }

    [Fact]
    public void ClosedNestedGenericImplementation_AndArrayDependency_AreSupported()
    {
        var result = TestHelper.Generate("""
            using System;
            using TrueMoon.Services;
            public class Outer<T> {
                internal class Service<U>(int[] items) { public int[] Items { get; } = items; }
            }
            public static class Graph {
                public static object[] Run() {
                    var items = new[] { 3, 5 };
                    using var ownership = (IDisposable)new TrueMoon.Cobalt.CobaltServiceResolverBuilder().Build(null!, [(_, c) => c
                        .Instance<int[]>(items).Transient<Outer<int>.Service<string>>()]);
                    return [ReferenceEquals(items, ((IServiceResolver)ownership).Resolve<Outer<int>.Service<string>>().Items)];
                }
            }
            """);
        Assert.Equal(new object[] { true }, Execute(result));
    }

    [Fact]
    public void OpenGenericInsideNonGenericContainer_IsSupported()
    {
        var result = TestHelper.Generate("""
            using System;
            using TrueMoon.Services;
            internal class Outer {
                public interface IService<T> { int Count { get; } }
                public class Service<T>(int count = 42) : IService<T> { public int Count { get; } = count; }
            }
            public static class Graph {
                public static object[] Run() {
                    using var ownership = (IDisposable)new TrueMoon.Cobalt.CobaltServiceResolverBuilder().Build(null!, [(_, c) => c
                        .Singleton(typeof(Outer.IService<>), typeof(Outer.Service<>))]);
                    var services = (IServiceResolver)ownership;
                    var first = services.Resolve<Outer.IService<string>>();
                    return [first.Count, ReferenceEquals(first, services.Resolve<Outer.IService<string>>()),
                        !ReferenceEquals(first, services.Resolve<Outer.IService<int>>())];
                }
            }
            """);
        Assert.Equal(new object[] { 42, true, true }, Execute(result));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ConstructorSelection_UsesUniqueLongestPublic_WithConsistentDefaultsAndNullability(bool open, bool registered)
    {
        var declaration = open ? "Service<T>" : "Service";
        var constructorName = "Service";
        var type = open ? "Service<>" : "Service";
        var resolved = open ? "Service<int>" : "Service";
        var registrations = registered ? "c.Instance<string>(\"registered\").Instance<int>(0).Instance<Mode>(Mode.Registered).Instance<decimal>(0m).Instance<Dependency>(new Dependency());" : "";
        var result = TestHelper.Generate($$"""
            using System;
            using TrueMoon.Services;
            public enum Mode { Default = 7, Registered = 9 }
            public class Dependency;
            internal class {{declaration}} {
                public string Choice { get; }
                public string Name { get; }
                public int Count { get; }
                public Mode Mode { get; }
                public decimal Price { get; }
                public Dependency? Dependency { get; }
                public {{constructorName}}() { Choice = "short"; Name = "short"; }
                private {{constructorName}}(int a, int b, int c, int d, int e, int f) { Choice = "private"; Name = "private"; }
                public {{constructorName}}(Dependency? dependency, string name = "fallback", int count = 23, Mode mode = Mode.Default, decimal price = 1.25m) {
                    Choice = "long"; Name = name; Count = count; Mode = mode; Price = price; Dependency = dependency;
                }
            }
            public static class Graph {
                public static object[] Run() {
                    using var ownership = (IDisposable)new TrueMoon.Cobalt.CobaltServiceResolverBuilder().Build(null!, [(_, c) => {
                        {{registrations}}
                        c.Singleton(typeof({{type}}), typeof({{type}}));
                    }]);
                    var services = (IServiceResolver)ownership;
                    var item = services.Resolve<{{resolved}}>();
                    return [item.Choice, item.Name, item.Count, (int)item.Mode, item.Price,
                        item.Dependency != null, ReferenceEquals(item, services.Resolve<{{resolved}}>())];
                }
            }
            """);
        var actual = Execute(result);
        Assert.Equal(new object[] { "long", registered ? "registered" : "fallback", registered ? 0 : 23,
            registered ? 9 : 7, registered ? 0m : 1.25m, registered, true }, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingRequiredParameter_DoesNotFallBackToShorterConstructor(bool open)
    {
        var declaration = open ? "Service<T>" : "Service";
        var type = open ? "Service<>" : "Service";
        var resolved = open ? "Service<int>" : "Service";
        var result = TestHelper.Generate($$"""
            using System;
            using TrueMoon.Services;
            public class Dependency;
            public class {{declaration}} {
                public Service() {}
                public Service(Dependency required) {}
            }
            public static class Graph {
                public static object[] Run() {
                    using var ownership = (IDisposable)new TrueMoon.Cobalt.CobaltServiceResolverBuilder().Build(null!, [(_, c) => c
                        .Transient(typeof({{type}}), typeof({{type}}))]);
                    try { ((IServiceResolver)ownership).Resolve<{{resolved}}>(); return [false, "no error"]; }
                    catch (TrueMoon.Cobalt.ServiceResolvingException error) { return [true, error.Message.Contains("Dependency")]; }
                }
            }
            """);
        Assert.Equal(new object[] { true, true }, Execute(result));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetsRequiredMembersConstructor_IsSupportedForClosedAndOpenTypes(bool open)
    {
        var declaration = open ? "Service<T>" : "Service";
        var type = open ? "Service<>" : "Service";
        var resolved = open ? "Service<int>" : "Service";
        var result = TestHelper.Generate($$"""
            using System;
            using TrueMoon.Services;
            public class {{declaration}} {
                public required string Name { get; init; }
                [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
                public Service() { Name = "initialized"; }
            }
            public static class Graph {
                public static object[] Run() {
                    using var ownership = (IDisposable)new TrueMoon.Cobalt.CobaltServiceResolverBuilder().Build(null!, [(_, c) => c
                        .Transient(typeof({{type}}), typeof({{type}}))]);
                    var services = (IServiceResolver)ownership;
                    var first = services.Resolve<{{resolved}}>();
                    return [first.Name, !ReferenceEquals(first, services.Resolve<{{resolved}}>())];
                }
            }
            """);
        Assert.Equal(new object[] { "initialized", true }, Execute(result));
    }

    private static void AssertDiagnostic(string source, string registration, string id, bool allowUnsafe = false)
    {
        var result = TestHelper.Generate(source, allowUnsafe);
        var diagnostic = Assert.Single(result.Run.Diagnostics);
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(registration, source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
        Assert.Contains("factory", diagnostic.GetMessage(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Service", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(result.Run.GeneratedTrees);
        Assert.Empty(result.Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    private static object[] Execute((GeneratorDriverRunResult Run, Compilation Output) result)
    {
        Assert.Empty(result.Run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        using var stream = new MemoryStream();
        var emit = result.Output.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        return (object[])assembly.GetType("Graph")!.GetMethod("Run")!.Invoke(null, null)!;
    }
}
