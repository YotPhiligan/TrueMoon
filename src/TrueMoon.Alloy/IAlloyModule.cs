using TrueMoon.Modules;

namespace TrueMoon.Alloy;

public interface IAlloyModule : IModule
{
    PresentationConfiguration Configuration { get; }
}