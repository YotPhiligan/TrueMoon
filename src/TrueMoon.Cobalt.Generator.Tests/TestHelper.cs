using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TrueMoon.Threading;

namespace TrueMoon.Cobalt.Generator.Tests;

public static class TestHelper
{
    public static Task Verify(string source)
    {
        // Parse the provided string into a C# syntax tree
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Create a Roslyn compilation for the syntax tree.
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: [syntaxTree], 
            references: [
                MetadataReference.CreateFromFile(typeof(IFactoryContainer).Assembly.Location), 
                MetadataReference.CreateFromFile(typeof(App).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IApp).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TmTaskScheduler).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.JitInfo).Assembly.Location),
            ]);


        // Create an instance of our EnumGenerator incremental source generator
        var generator = new CobaltGenerator();

        // The GeneratorDriver is used to run our generator against a compilation
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Run the source generator!
        driver = driver.RunGenerators(compilation);

        // Use verify to snapshot test the source generator output!
        return Verifier.Verify(driver);
    }
}