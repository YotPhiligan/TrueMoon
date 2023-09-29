using Microsoft.CodeAnalysis;

namespace TrueMoon.Thorium.Generator;

public class GenerationContext
{
    private const string NamespacePart = "TrueMoon.Generated";
    
    public GenerationContext(SourceProductionContext sourceProductionContext, string assembly)
    {
        SourceProductionContext = sourceProductionContext;
        
        NamespacePrefix = $"{NamespacePart}.{assembly}";
    }

    public string NamespacePrefix { get; }
    public SourceProductionContext SourceProductionContext { get; }
    public List<ITypeSymbol> Types { get; } = new List<ITypeSymbol>();

    public SourceHandle CreateSourceHandle(INamedTypeSymbol symbol, string sourceNamePostfix = "GeneratedImplementation")
    {
        var handle = new SourceHandle(this, symbol, sourceNamePostfix);
        return handle;
    }

    public bool CheckType(ITypeSymbol type)
    {
        if (Types.Contains(type))
        {
            return true;
        }
        Types.Add(type);
        return false;
    }
}