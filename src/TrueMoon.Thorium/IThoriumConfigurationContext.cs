namespace TrueMoon.Thorium;

public interface IThoriumConfigurationContext
{
    ThoriumConfiguration Configuration { get; }
    IAppCreationContext AppCreationContext { get; }
}