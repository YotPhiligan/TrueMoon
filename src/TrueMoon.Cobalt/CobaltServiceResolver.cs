using System.Collections.Frozen;
using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class CobaltServiceResolver : CobaltServiceResolverBase
{
    public CobaltServiceResolver(FrozenDictionary<Type, ITypeContainer> resolversContainers) : base(resolversContainers)
    {
        
    }
}