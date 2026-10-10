using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TrueMoon.Cobalt.Generator.Tests;

public static class TestHelper
{
    public static (GeneratorDriverRunResult Run, Compilation Output) Generate(string source)
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat([typeof(IFactoryContainer).Assembly.Location, typeof(TrueMoon.Services.IServiceResolver).Assembly.Location])
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var compilation = CSharpCompilation.Create("GeneratorTests" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)], paths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new CobaltGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult(), output);
    }

    public static GeneratorDriverRunResult Verify(string source) => Generate(source).Run;
}
