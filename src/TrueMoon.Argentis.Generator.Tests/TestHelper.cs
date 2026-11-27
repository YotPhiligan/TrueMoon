using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TrueMoon.Argentis.Generator.Tests;

public static partial class TestHelper
{
    public static (GeneratorDriverRunResult Run, Compilation Output) Generate(string source)
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat([typeof(TrueMoon.Argentis.Views.View).Assembly.Location,
                     typeof(TrueMoon.Alloy.Hosting.ViewRegistrationExtensions).Assembly.Location])
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var parseOptions = CSharpParseOptions.Default;
        var compilation = CSharpCompilation.Create("GeneratorTests" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parseOptions)], paths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ViewPartsGenerator().AsSourceGenerator(), new ViewRegistrationGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult(), output);
    }

    public static GeneratorDriverRunResult Verify(string source) => Generate(source).Run;

    public static string? GeneratedFile(GeneratorDriverRunResult run, string hintName)
    {
        var tree = run.GeneratedTrees.SingleOrDefault(candidate =>
            Path.GetFileName(candidate.FilePath).EndsWith(hintName, StringComparison.Ordinal));
        return tree?.ToString();
    }
}
