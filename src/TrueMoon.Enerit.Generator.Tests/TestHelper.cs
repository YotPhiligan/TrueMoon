using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TrueMoon.Enerit.Generator.Tests;

public static class TestHelper
{
    public static GeneratorDriverRunResult Verify(string source)
    {
        // Parse the provided string into a C# syntax tree
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Create a Roslyn compilation for the syntax tree.
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: [syntaxTree]);


        // Create an instance of our EnumGenerator incremental source generator
        var generator = new ServicesGenerator();

        // The GeneratorDriver is used to run our generator against a compilation
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Run the source generator!
        driver = driver.RunGenerators(compilation);

        // Use verify to snapshot test the source generator output!
        return driver.GetRunResult();
    }
}