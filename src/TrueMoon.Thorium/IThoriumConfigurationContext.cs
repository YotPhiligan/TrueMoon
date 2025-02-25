namespace TrueMoon.Thorium;

public interface IThoriumConfigurationContext
{
    ThoriumConfiguration Configuration { get; }
    IAppConfigurationContext AppConfigurationContext { get; }
}