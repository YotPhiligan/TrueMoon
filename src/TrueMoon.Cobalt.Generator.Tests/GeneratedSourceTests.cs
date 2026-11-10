using Microsoft.CodeAnalysis;
using Xunit;

namespace TrueMoon.Cobalt.Generator.Tests;

public class GeneratedSourceTests
{
    [Theory]
    [InlineData(OptimizationLevel.Debug)]
    [InlineData(OptimizationLevel.Release)]
    public void HintNames_AreBoundedDistinctStable_AndWritableForLongUnicodeNestedGenericTypes(OptimizationLevel optimization)
    {
        var first = TestHelper.Generate(Source(), optimizationLevel: optimization);
        var second = TestHelper.Generate(Source(reverse: true), optimizationLevel: optimization);
        AssertCompiles(first);
        AssertCompiles(second);
        var sources = Assert.Single(first.Run.Results).GeneratedSources;
        Assert.Equal(10, sources.Length); // Nine distinct resolvers plus the initializer; the repeated call is deduplicated.
        Assert.Equal(10, sources.Select(s => s.HintName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(sources.Select(s => s.HintName).OrderBy(n => n),
            Assert.Single(second.Run.Results).GeneratedSources.Select(s => s.HintName).OrderBy(n => n));
        // Each key keeps its own name when registration order and assembly identity change.
        foreach (var source in sources.Where(s => s.HintName != "CobaltResolversRegistration.g.cs"))
        {
            var other = Assert.Single(Assert.Single(second.Run.Results).GeneratedSources.Where(s => s.HintName == source.HintName));
            Assert.Equal(source.SourceText.ToString().Split("internal class ")[1],
                other.SourceText.ToString().Split("internal class ")[1]);
        }

        var directory = Path.Combine(Path.GetTempPath(), "CobaltHints-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var source in sources)
            {
                Assert.InRange(source.HintName.Length, 1, 128);
                Assert.All(source.HintName, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character is '_' or '.'));
                var path = Path.Combine(directory, source.HintName);
                File.WriteAllText(path, source.SourceText.ToString());
                Assert.Equal(source.SourceText.ToString(), File.ReadAllText(path));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(OptimizationLevel.Debug)]
    [InlineData(OptimizationLevel.Release)]
    public void GeneratedRegistration_WithDocumentationAndCS1591AsError_CompilesAndInitializesGraph(OptimizationLevel optimization)
    {
        var result = TestHelper.Generate("""
            using System;
            using TrueMoon.Services;
            internal interface IAlias;
            internal interface IOtherAlias;
            internal class Owned : IAlias, IOtherAlias, IDisposable {
                public int Disposals;
                public void Dispose() => Disposals++;
            }
            internal class Transient;
            internal static class Graph {
                public static bool Run() {
                    var resolver = new TrueMoon.Cobalt.CobaltServiceResolverBuilder().Build(null!, [(_, c) => c
                        .Composite<Owned, IAlias, IOtherAlias>().Transient<Transient>()]);
                    var first = resolver.Resolve<Owned>();
                    var valid = ReferenceEquals(first, resolver.Resolve<Owned>()) &&
                        ReferenceEquals(first, resolver.Resolve<IAlias>()) &&
                        ReferenceEquals(first, resolver.Resolve<IOtherAlias>()) &&
                        !ReferenceEquals(resolver.Resolve<Transient>(), resolver.Resolve<Transient>()) && first.Disposals == 0;
                    ((IDisposable)resolver).Dispose();
                    return valid && first.Disposals == 1;
                }
            }
            """, documentation: true, optimizationLevel: optimization);
        AssertCompiles(result);
        using var pe = new MemoryStream();
        using var xml = new MemoryStream();
        var emit = result.Output.Emit(pe, xmlDocumentationStream: xml);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        Assert.DoesNotContain(emit.Diagnostics, d => d.Id == "CS1591");
        var docs = System.Text.Encoding.UTF8.GetString(xml.ToArray());
        Assert.Contains("CobaltResolversRegistration\"", docs);
        Assert.Contains("CobaltResolversRegistration.Init\"", docs);
        var assembly = System.Reflection.Assembly.Load(pe.ToArray());
        Assert.Equal(true, assembly.GetType("Graph")!.GetMethod("Run")!.Invoke(null, null));
    }

    [Fact]
    public void ConsumerDocumentationErrors_AreStillReportedAtConsumerDeclaration()
    {
        var result = TestHelper.Generate("""
            using TrueMoon.Services;
            public class Undocumented;
            internal class Service;
            internal class Graph {
                public void Register(IServicesRegistrationContext context) => context.Singleton<Service>();
            }
            """, documentation: true);
        Assert.Empty(result.Run.Diagnostics);
        var diagnostic = Assert.Single(result.Output.GetDiagnostics().Where(d => d.Id == "CS1591"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Undocumented", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
        using var pe = new MemoryStream();
        Assert.False(result.Output.Emit(pe).Success);
    }

    private static void AssertCompiles((GeneratorDriverRunResult Run, Compilation Output) result)
    {
        Assert.Empty(result.Run.Diagnostics);
        Assert.Empty(result.Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    private static string Source(bool reverse = false)
    {
        var common = new string('A', 65);
        var registrations = new[]
        {
            $"Singleton<IFactory<Outer.TitleBar<int>, Outer.Arguments>, {common}FactoryOne>()",
            $"Transient<IFactory<Outer.TitleBar<int>, Outer.Arguments>, {common}FactoryOne>()",
            $"Singleton<IFactory<Outer.TitleBar<int>, Outer.Arguments>, {common}FactoryTwo>()",
            "Singleton<Outer.Сервис>()", "Singleton<Outer.服务>()",
            "Singleton(typeof(Outer.IBox<>), typeof(Outer.Box<>))",
            $"Composite<{common}Both, Outer.IOne, Outer.ITwo>()",
            $"Singleton<IFactory<Outer.TitleBar<int>, Outer.Arguments>, {common}FactoryOne>()"
        };
        return $$"""
            using TrueMoon;
            using TrueMoon.Services;
            namespace Consumer.Presentation.Shell.WithLongNamespaces.ForGeneratedResolvers.AndGenericFactoryRegistrations;
            internal class Outer {
                public class TitleBar<T>;
                public class Arguments;
                public class Сервис;
                public class 服务;
                public interface IBox<T>;
                public class Box<T> : IBox<T>;
                public interface IOne;
                public interface ITwo;
            }
            internal class {{common}}Both : Outer.IOne, Outer.ITwo;
            internal class {{common}}FactoryOne : IFactory<Outer.TitleBar<int>, Outer.Arguments> {
                public Outer.TitleBar<int> Create(Outer.Arguments arguments) => new();
            }
            internal class {{common}}FactoryTwo : IFactory<Outer.TitleBar<int>, Outer.Arguments> {
                public Outer.TitleBar<int> Create(Outer.Arguments arguments) => new();
            }
            internal class Graph {
                public void Register(IServicesRegistrationContext context) => context
                    .{{string.Join(".", reverse ? registrations.Reverse() : registrations)}};
            }
            """;
    }
}
