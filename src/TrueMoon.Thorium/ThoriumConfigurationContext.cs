namespace TrueMoon.Thorium;

public class ThoriumConfigurationContext : IThoriumConfigurationContext
{
    public ThoriumConfigurationContext(IAppConfigurationContext appConfigurationContext)
    {
        Configuration = new ThoriumConfiguration();
        AppConfigurationContext = appConfigurationContext;
    }
    
    public ThoriumConfiguration Configuration { get; }
    public IAppConfigurationContext AppConfigurationContext { get; }
}