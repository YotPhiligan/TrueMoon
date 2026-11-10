using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TrueMoon.Cobalt.Generator.Tests;

public static class TestHelper
{
    public static (GeneratorDriverRunResult Run, Compilation Output) Generate(string source, bool allowUnsafe = false,
        bool documentation = false, OptimizationLevel optimizationLevel = OptimizationLevel.Debug)
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat([typeof(IFactoryContainer).Assembly.Location, typeof(TrueMoon.Services.IServiceResolver).Assembly.Location])
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var parseOptions = CSharpParseOptions.Default.WithDocumentationMode(documentation ? DocumentationMode.Diagnose : DocumentationMode.Parse);
        var compilation = CSharpCompilation.Create("GeneratorTests" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parseOptions)], paths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable,
                allowUnsafe: allowUnsafe, optimizationLevel: optimizationLevel,
                specificDiagnosticOptions: documentation ? new Dictionary<string, ReportDiagnostic> { ["CS1591"] = ReportDiagnostic.Error } : null));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new CobaltGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult(), output);
    }

    public static GeneratorDriverRunResult Verify(string source) => Generate(source).Run;
}
