using Microsoft.CodeAnalysis;

namespace TrueMoon.Cobalt.Generator;

public class ResolverSourceItem
{
    public string ServiceName { get; set; }
    public ResolvingServiceCreationType CreationType { get; set; }
    public string FactoryCode { get; set; }
    public INamedTypeSymbol? AdditionalType { get; set; }
    public string Source { get; set; }
    public INamedTypeSymbol? ServiceTypeSymbol { get; set; }
    public INamedTypeSymbol? ImplementationTypeSymbol { get; set; }
}