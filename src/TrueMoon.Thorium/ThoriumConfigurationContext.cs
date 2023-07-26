namespace TrueMoon.Thorium;

public class ThoriumConfigurationContext : IThoriumConfigurationContext
{
    public ThoriumConfigurationContext(IAppCreationContext appCreationContext)
    {
        Configuration = new ThoriumConfiguration();
        AppCreationContext = appCreationContext;
    }
    
    public ThoriumConfiguration Configuration { get; }
    public IAppCreationContext AppCreationContext { get; }
}