using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class CoreBoundaryTests
{
    [Fact]
    public void Alloy_AssemblyReferences_ContainOnlySharedContractsArgentisAndBcl()
    {
        var references = typeof(UiSession).Assembly.GetReferencedAssemblies();

        Assert.Contains(references, reference => reference.Name == "TrueMoon.Argentis");
        Assert.Contains(references, reference => reference.Name == "TrueMoon.Contracts");
        Assert.All(references, reference => Assert.True(reference.Name == "TrueMoon.Argentis"
            || reference.Name == "TrueMoon.Contracts"
            || reference.Name?.StartsWith("System.", StringComparison.Ordinal) == true,
            $"Unexpected Alloy dependency: {reference.Name}"));
    }

    [Fact]
    public void Argentis_AssemblyReferences_DoNotDependOnConcreteModules()
    {
        var references = typeof(Element).Assembly.GetReferencedAssemblies();

        Assert.All(references, reference => Assert.True(reference.Name == "TrueMoon.Contracts" || reference.Name?.StartsWith("System.", StringComparison.Ordinal) == true,
            $"Unexpected Argentis dependency: {reference.Name}"));
    }
}
