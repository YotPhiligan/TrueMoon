using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;

namespace TrueMoon.Cobalt.Generator.Tests;

public class GeneratorTests
{
    private static (GeneratorDriverRunResult Run, Compilation Output) Generate(string source)
    {
        var result = TestHelper.Generate(source);
        Assert.Empty(result.Run.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(result.Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        return result;
    }

    [Fact]
    public void GeneratesExactRegistrations_ForEachServiceImplementationAndLifetime()
    {
        var result = Generate("""
            using TrueMoon.Services;
            public interface IOne;
            public interface ITwo;
            public class First : IOne, ITwo;
            public class Second : IOne;
            public class Graph {
                public void Register(IServicesRegistrationContext context) => context
                    .Singleton<IOne, First>().Transient<IOne, First>()
                    .Singleton<IOne, Second>().Singleton<ITwo, First>();
            }
            """);
        Assert.Equal(5, result.Run.GeneratedTrees.Length);
        var registration = result.Run.GeneratedTrees.Single(t => t.FilePath.EndsWith("CobaltResolversRegistration.g.cs")).ToString();
        Assert.Contains("typeof(global::IOne), typeof(global::First), global::TrueMoon.Cobalt.ServiceLifetime.Singleton", registration);
        Assert.Contains("typeof(global::IOne), typeof(global::First), global::TrueMoon.Cobalt.ServiceLifetime.Transient", registration);
        Assert.Contains("typeof(global::IOne), typeof(global::Second)", registration);
        Assert.Contains("typeof(global::ITwo), typeof(global::First)", registration);
    }

    [Fact]
    public void RuntimeFactories_DoNotPublishGeneratedConstructionOrSelfAliases()
    {
        var result = Generate("""
            using TrueMoon.Services;
            public interface IService;
            public class Graph {
                public void Register(IServicesRegistrationContext context) => context
                    .Singleton<IService>(_ => null!).Transient<IService>(_ => null!);
            }
            """);
        Assert.Empty(result.Run.GeneratedTrees);
    }

    [Fact]
    public void CompositeAliases_ResolveSharedConcreteSingleton_InGeneratedCode()
    {
        var result = Generate("""
            using TrueMoon.Services;
            public interface IOne;
            public interface ITwo;
            public class Both : IOne, ITwo;
            public class Graph {
                public void Register(IServicesRegistrationContext context) => context.Composite<Both, IOne, ITwo>();
                public static IServiceResolver Create() => new TrueMoon.Cobalt.CobaltServiceResolverBuilder().Build(null!, [(_, c) => c.Composite<Both, IOne, ITwo>()]);
            }
            """);
        Assert.Equal(4, result.Run.GeneratedTrees.Length);
        var aliases = result.Run.GeneratedTrees.Where(t => t.FilePath.EndsWith("AliasResolver.g.cs")).ToArray();
        Assert.Equal(2, aliases.Length);
        Assert.All(aliases, tree => Assert.Contains("context.Resolve<global::Both>()", tree.ToString()));
        Assert.Single(result.Run.GeneratedTrees.Where(t => t.ToString().Contains("new global::Both(")));
        using var stream = new MemoryStream();
        var emit = result.Output.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        var services = (TrueMoon.Services.IServiceResolver)assembly.GetType("Graph")!.GetMethod("Create")!.Invoke(null, null)!;
        using var ownership = (IDisposable)services;
        var concrete = services.GetService(assembly.GetType("Both")!);
        var aliasTypes = assembly.GetTypes().Where(t => t.Name.EndsWith("AliasResolver")).ToArray();
        Assert.Equal(2, aliasTypes.Length);
        Assert.All(aliasTypes, type =>
        {
            var resolver = Activator.CreateInstance(type);
            var method = type.GetMethod("Resolve")!;
            Assert.Same(concrete, method.Invoke(resolver, [services]));
            Assert.Same(concrete, method.Invoke(resolver, [services]));
        });
    }

    [Fact]
    public void SameShortTypeNames_AndRepeatedCalls_GenerateDistinctCompilableResolvers()
    {
        var result = Generate("""
            using TrueMoon.Services;
            namespace One { public class Service; }
            namespace Two { public class Service; }
            public class Graph {
                public void Register(IServicesRegistrationContext context) => context
                    .Singleton<One.Service>().Singleton<Two.Service>().Singleton<One.Service>();
            }
            """);
        Assert.Equal(3, result.Run.GeneratedTrees.Length);
        Assert.Equal(3, result.Run.GeneratedTrees.Select(t => t.FilePath).Distinct().Count());
        Assert.Single(result.Run.GeneratedTrees.Where(t => t.ToString().Contains("new global::One.Service(")));
        Assert.Single(result.Run.GeneratedTrees.Where(t => t.ToString().Contains("new global::Two.Service(")));
    }

    [Fact]
    public void OpenGenericConstructors_ResolveClosedDependencies_AndRetainSingletonIdentity()
    {
        var result = Generate("""
            using System;
            using TrueMoon.Services;
            using TrueMoon.Cobalt;
            public interface IBox<T>;
            public class Box<T> : IBox<T>;
            public interface IRepository<T> { IBox<T> Box { get; } }
            public class Repository<T>(IBox<T> box) : IRepository<T> { public IBox<T> Box { get; } = box; }
            public static class Graph {
                public static bool Run() {
                    using var resolver = (IDisposable)new CobaltServiceResolverBuilder().Build(null!, [(_, c) => c
                        .Singleton(typeof(IBox<>), typeof(Box<>))
                        .Singleton(typeof(IRepository<>), typeof(Repository<>))]);
                    var services = (IServiceResolver)resolver;
                    var first = services.Resolve<IRepository<string>>();
                    return ReferenceEquals(first, services.Resolve<IRepository<string>>()) &&
                        ReferenceEquals(first.Box, services.Resolve<IBox<string>>()) &&
                        !ReferenceEquals(first.Box, services.Resolve<IBox<int>>());
                }
            }
            """);
        using var stream = new MemoryStream();
        var emit = result.Output.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        var actual = assembly.GetType("Graph")!.GetMethod("Run")!.Invoke(null, null);
        Assert.Equal(true, actual);
    }

    [Fact]
    public void ClosedTypeOverloads_EmitConcreteConstructors()
    {
        var result = Generate("""
            using TrueMoon.Services;
            public interface IService;
            public class Concrete : IService;
            public class Graph {
                public void Register(IServicesRegistrationContext context) => context.Singleton(typeof(IService), typeof(Concrete));
            }
            """);
        Assert.Equal(2, result.Run.GeneratedTrees.Length);
        Assert.Single(result.Run.GeneratedTrees.Where(t => t.ToString().Contains("new global::Concrete(")));
        Assert.DoesNotContain(result.Run.GeneratedTrees, tree => tree.ToString().Contains("MakeGenericType"));
    }
}
