using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TrueMoon.Enerit.IO;

namespace TrueMoon.Enerit.Generator.Tests;

public class PayloadContractTests
{
    [Theory]
    [InlineData("Task<string?> Echo(string? value);")]
    [InlineData("Task<int?> Echo(int? value);")]
    [InlineData("Task<int[]?> Echo(int[]? value);")]
    [InlineData("Task<List<int?>?> Echo(List<int?>? value);")]
    [InlineData("Task<IReadOnlyList<string?>?> Echo(IReadOnlyList<string?>? value);")]
    [InlineData("Task<Dictionary<string, int>?> Echo(Dictionary<string, int>? value);")]
    [InlineData("Task<IReadOnlyDictionary<string, int>?> Echo(IReadOnlyDictionary<string, int>? value);")]
    [InlineData("Task<Memory<byte>> Echo(Memory<byte> value);")]
    [InlineData("Task<ReadOnlyMemory<byte>> Echo(ReadOnlyMemory<byte> value);")]
    [InlineData("Task<Payload?> Echo(Payload? value);")]
    [InlineData("Task<Guid> Echo(Guid value); Task<DateTime> Date(DateTime value); Task<TimeSpan> Span(TimeSpan value);")]
    [InlineData("Task<Kind?> Echo(Kind? value); Task<DateTimeOffset> Date(DateTimeOffset value);")]
    public void SupportedPayloads_EmitCompilableClientAndHandler(string methods)
    {
        var (result, compilation, diagnostics) = Generate(methods);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Null(result.Exception);
        Assert.Contains(result.GeneratedSources, s => s.HintName == "TestServiceGeneratedImplementation.g.cs");
        Assert.Contains(result.GeneratedSources, s => s.HintName == "TestServiceInvocationServerHandler.g.cs");
        using var output = new MemoryStream();
        var emission = compilation.Emit(output);
        Assert.True(emission.Success, string.Join(Environment.NewLine, emission.Diagnostics));
    }

    [Theory]
    [InlineData("Task<object> Echo(object value);")]
    [InlineData("Task<decimal> Echo(decimal value);")]
    [InlineData("ValueTask<int> Echo(int value);")]
    [InlineData("Task<List<List<int>>> Echo(List<List<int>> value);")]
    [InlineData("T Echo<T>(T value);")]
    [InlineData("void Echo(ref int value);")]
    [InlineData("int Value { get; }")]
    [InlineData("Task<Cyclic> Echo(Cyclic value);")]
    [InlineData("Task<Immutable> Echo(Immutable value);")]
    [InlineData("Task<Derived> Echo(Derived value);")]
    [InlineData("Task<Constructed> Echo(Constructed value);")]
    public void UnsupportedPayloads_ReportTme001WithoutBrokenServiceSources(string methods)
    {
        var (result, _, diagnostics) = Generate(methods);
        Assert.Null(result.Exception);
        var error = Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal("TME001", error.Id);
        Assert.Contains("Consumer.ITestService", error.GetMessage());
        Assert.True(error.Location.IsInSource);
        Assert.DoesNotContain(result.GeneratedSources, s => s.HintName.Contains("TestService", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void MethodCodeLimit_AcceptsLastByteAndRejectsOverflow(int count, bool accepted)
    {
        var (result, compilation, diagnostics) = Generate(string.Join(" ", Enumerable.Range(0, count).Select(i => $"void Method{i}();")));
        Assert.Null(result.Exception);
        if (!accepted)
        {
            Assert.Equal("TME001", Assert.Single(diagnostics, d => d.Severity == DiagnosticSeverity.Error).Id);
            Assert.DoesNotContain(result.GeneratedSources, s => s.HintName.Contains("TestService", StringComparison.Ordinal));
            return;
        }
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var client = result.GeneratedSources.Single(s => s.HintName == "TestServiceGeneratedImplementation.g.cs").SourceText.ToString();
        Assert.Contains("_invocationClient.Invoke(255", client);
        using var output = new MemoryStream();
        var emission = compilation.Emit(output);
        Assert.True(emission.Success, string.Join(Environment.NewLine, emission.Diagnostics));
    }

    private static (GeneratorRunResult Result, Compilation Compilation, Diagnostic[] Diagnostics) Generate(string methods)
    {
        var source = $$"""
            #nullable enable
            global using System;
            global using System.Collections.Generic;
            global using System.Threading;
            global using System.Threading.Tasks;
            namespace Consumer;
            public enum Kind { None, First }
            public class Payload
            {
                public int Id { get; set; }
                public string? Text { get; set; }
                public List<int?>? Values { get; set; }
                public Dictionary<string, int>? Tags { get; set; }
                public Memory<byte> Bytes { get; set; }
            }
            public class Cyclic { public Cyclic? Next { get; set; } }
            public class Immutable { public int Value { get; } }
            public class Derived : Payload { public int Extra { get; set; } }
            public class Constructed { public Constructed(int value) { Value = value; } public int Value { get; set; } }
            public interface ITestService { {{methods}} }
            public static class Registration
            {
                public static void Register() => UseInvocationService<ITestService>();
                private static void UseInvocationService<T>() { }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(IInvocationClient).Assembly.Location)
            .Append(typeof(TrueMoon.Diagnostics.IEventsSource<>).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("PayloadConsumer", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ServicesGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var diagnostics);
        return (Assert.Single(driver.GetRunResult().Results), generated, diagnostics.ToArray());
    }
}
