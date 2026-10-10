using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TrueMoon.Enerit.IO;

namespace TrueMoon.Enerit.Generator.Tests;

public class ServicesGeneratorContractTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("inherited")]
    [InlineData("diamond")]
    [InlineData("duplicate-signatures")]
    [InlineData("overloads")]
    public void GeneratedClientAndHandler_ImplementTheCompleteServiceInterface(string inheritance)
    {
        var serviceDeclaration = inheritance switch
        {
            "direct" => "public interface ITestService { Task<int> AddAsync(int left, int right, CancellationToken cancellationToken = default); Task<string> EchoAsync(string value); }",
            "inherited" => "public interface IBaseService { Task<int> AddAsync(int left, int right, CancellationToken cancellationToken = default); } public interface ITestService : IBaseService { Task<string> EchoAsync(string value); }",
            "diamond" => "public interface IBaseService { Task<int> AddAsync(int left, int right, CancellationToken cancellationToken = default); } public interface ILeftService : IBaseService {} public interface IRightService : IBaseService {} public interface ITestService : ILeftService, IRightService { Task<string> EchoAsync(string value); }",
            "duplicate-signatures" => "public interface ILeftService { Task<int> AddAsync(int left, int right, CancellationToken cancellationToken = default); } public interface IRightService { Task<int> AddAsync(int first, int second, CancellationToken token = default); } public interface ITestService : ILeftService, IRightService { Task<string> EchoAsync(string value); }",
            "overloads" => "public interface ILeftService { Task<int> AddAsync(int left, int right, CancellationToken cancellationToken = default); } public interface IRightService { Task<int> AddAsync(string first, string second); } public interface ITestService : ILeftService, IRightService { Task<string> EchoAsync(string value); }",
            _ => throw new ArgumentOutOfRangeException(nameof(inheritance))
        };
        var source = $$"""
            global using System.Threading;
            global using System.Threading.Tasks;
            namespace Consumer;
            {{serviceDeclaration}}
            public static class Registration
            {
                public static void Register() => UseInvocationService<ITestService>();
                private static void UseInvocationService<T>() {}
            }
            """;
        var runtimeReferences = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(IInvocationClient).Assembly.Location)
            .Append(typeof(TrueMoon.Diagnostics.IEventsSource<>).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("EneritContractConsumer",
            [CSharpSyntaxTree.ParseText(source)], runtimeReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        AssertNoErrors(compilation.GetDiagnostics());
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ServicesGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var generatedCompilation, out var diagnostics);

        AssertNoErrors(diagnostics);
        var generatorResult = Assert.Single(driver.GetRunResult().Results);
        Assert.Null(generatorResult.Exception);
        Assert.Contains(generatorResult.GeneratedSources, source => source.HintName == "TestServiceGeneratedImplementation.g.cs");
        Assert.Contains(generatorResult.GeneratedSources, source => source.HintName == "TestServiceInvocationServerHandler.g.cs");
        Assert.Contains(generatorResult.GeneratedSources, source => source.HintName == "InvocationServiceInitializer.g.cs");
        AssertNoErrors(generatedCompilation.GetDiagnostics());
        var client = generatorResult.GeneratedSources.Single(source => source.HintName == "TestServiceGeneratedImplementation.g.cs").SyntaxTree.GetRoot();
        var handler = generatorResult.GeneratedSources.Single(source => source.HintName == "TestServiceInvocationServerHandler.g.cs").SyntaxTree.GetRoot();
        var clientCodes = client.DescendantNodes().OfType<MethodDeclarationSyntax>().Select(method =>
        {
            var invocation = Assert.Single(method.DescendantNodes().OfType<InvocationExpressionSyntax>(), call => call.Expression is MemberAccessExpressionSyntax access &&
                               access.Expression.ToString() == "_invocationClient");
            var code = (LiteralExpressionSyntax)invocation.ArgumentList.Arguments[0].Expression;
            return $"{method.Identifier.ValueText}:{code.Token.Value}";
        }).Order(StringComparer.Ordinal).ToArray();
        var handlerCodes = handler.DescendantNodes().OfType<SwitchSectionSyntax>().Select(section =>
        {
            var label = Assert.Single(section.Labels.OfType<CaseSwitchLabelSyntax>());
            var invocation = Assert.Single(section.DescendantNodes().OfType<InvocationExpressionSyntax>(), call => call.Expression is MemberAccessExpressionSyntax access &&
                               access.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                                   .Any(identifier => identifier.Identifier.ValueText == "_service"));
            var method = ((MemberAccessExpressionSyntax)invocation.Expression).Name.Identifier.ValueText;
            return $"{method}:{((LiteralExpressionSyntax)label.Value).Token.Value}";
        }).Order(StringComparer.Ordinal).ToArray();
        var expectedCodes = inheritance switch
        {
            "direct" => new[] { "AddAsync:0", "EchoAsync:1" },
            "overloads" => new[] { "AddAsync:1", "AddAsync:2", "EchoAsync:0" },
            _ => new[] { "AddAsync:1", "EchoAsync:0" }
        };
        Assert.Equal(expectedCodes, clientCodes);
        Assert.Equal(expectedCodes, handlerCodes);
        using var assembly = new MemoryStream();
        var emission = generatedCompilation.Emit(assembly);
        Assert.True(emission.Success, string.Join(Environment.NewLine, emission.Diagnostics));
    }

    private static void AssertNoErrors(IEnumerable<Diagnostic> diagnostics)
    {
        var errors = diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
    }
}
