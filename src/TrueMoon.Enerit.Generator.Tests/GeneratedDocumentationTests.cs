using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TrueMoon.Enerit.IO;

namespace TrueMoon.Enerit.Generator.Tests;

public class GeneratedDocumentationTests
{
    [Theory]
    [InlineData(OptimizationLevel.Debug)]
    [InlineData(OptimizationLevel.Release)]
    public void DocumentedGeneratedServices_CompileEmitXmlAndRegister(OptimizationLevel optimization)
    {
        var (result, output) = Generate(optimization);
        Assert.Null(result.Exception);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(5, result.GeneratedSources.Length);
        using var pe = new MemoryStream();
        using var xml = new MemoryStream();
        var emission = output.Emit(pe, xmlDocumentationStream: xml);
        Assert.True(emission.Success, string.Join(Environment.NewLine, emission.Diagnostics));
        Assert.DoesNotContain(emission.Diagnostics, d => d.Id == "CS1591");
        xml.Position = 0;
        var members = XDocument.Load(xml).Descendants("member").Select(m => (string)m.Attribute("name")!).ToArray();
        foreach (var type in new[] { "IdentitySerializationExtensions", "ReplySerializationExtensions",
                     "BackendGeneratedImplementation", "BackendInvocationServerHandler", "ModuleInitializer" })
            Assert.Contains(members, name => name.EndsWith("." + type, StringComparison.Ordinal));
        Assert.Contains(members, name => name.EndsWith(".ModuleInitializer.Init", StringComparison.Ordinal));
        Assert.Contains(members, name => name.Contains(".BackendGeneratedImplementation.HelloAsync(", StringComparison.Ordinal));
        Assert.Contains(members, name => name.Contains(".ReplySerializationExtensions.Deserialize(", StringComparison.Ordinal));
        Assert.Contains(members, name => name.Contains(".IdentitySerializationExtensions.Serialize(", StringComparison.Ordinal));

        var assembly = Assembly.Load(pe.ToArray());
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        var contract = assembly.GetType("Consumer.IBackend", throwOnError: true)!;
        foreach (var (method, implementation) in new[] { ("GetService", "BackendGeneratedImplementation"), ("GetHandler", "BackendInvocationServerHandler") })
        {
            var registered = typeof(InvocationServiceStorage).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(contract).Invoke(InvocationServiceStorage.Shared, null);
            Assert.Equal(assembly.GetTypes().Single(t => t.Name == implementation), registered);
        }
    }

    [Theory]
    [InlineData(OptimizationLevel.Debug)]
    [InlineData(OptimizationLevel.Release)]
    public void UndocumentedConsumerMember_RemainsCs1591Error(OptimizationLevel optimization)
    {
        var (result, output) = Generate(optimization, """
            /// <summary>A consumer-owned API.</summary>
            public class ConsumerApi { public void Undocumented() { } }
            """);
        Assert.Null(result.Exception);
        var diagnostic = Assert.Single(output.GetDiagnostics().Where(d => d.Id == "CS1591"));
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Consumer.cs", diagnostic.Location.SourceTree!.FilePath);
        Assert.Equal("Undocumented", diagnostic.Location.SourceTree.GetText().ToString(diagnostic.Location.SourceSpan));
        using var pe = new MemoryStream();
        Assert.False(output.Emit(pe).Success);
    }

    private static (GeneratorRunResult Result, Compilation Output) Generate(OptimizationLevel optimization, string extra = "")
    {
        var parse = CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.Diagnose);
        var source = CSharpSyntaxTree.ParseText(Source + Environment.NewLine + extra, parse, "Consumer.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(IInvocationClient).Assembly.Location)
            .Append(typeof(TrueMoon.Diagnostics.IEventsSource<>).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("DocumentationConsumer" + Guid.NewGuid().ToString("N"), [source], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: optimization,
                nullableContextOptions: NullableContextOptions.Enable,
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic> { ["CS1591"] = ReportDiagnostic.Error }));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new ServicesGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Assert.Empty(diagnostics);
        return (Assert.Single(driver.GetRunResult().Results), output);
    }

    private const string Source = """
        global using System;
        global using System.Threading;
        global using System.Threading.Tasks;
        namespace Consumer;
        /// <summary>Backend identity.</summary>
        public class Identity {
            /// <summary>Backend number.</summary>
            public int Id { get; set; }
        }
        /// <summary>Project reply.</summary>
        public class Reply {
            /// <summary>Project name.</summary>
            public string? Name { get; set; }
        }
        /// <summary>Backend contract.</summary>
        public interface IBackend {
            /// <summary>Reads identity.</summary>
            Task<Identity> HelloAsync(CancellationToken token = default);
            /// <summary>Opens a project.</summary>
            Task<Reply> OpenProjectAsync(string name, CancellationToken token = default);
            /// <summary>Returns an identity.</summary>
            Identity Echo(Identity value);
            /// <summary>Sends a notification.</summary>
            void Notify();
        }
        internal static class Registration {
            internal static void Configure() { UseInvocationService<IBackend>(); ListenInvocationService<IBackend>(); }
            private static void UseInvocationService<T>() { }
            private static void ListenInvocationService<T>() { }
        }
        """;
}
