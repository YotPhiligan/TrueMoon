namespace TrueMoon.Thorium;

public interface IThoriumModule : IModule
{
    void SetConfiguration(ThoriumConfiguration configuration);
    ThoriumConfiguration GetConfiguration();
    void ListenMessage<TMessage>();
}