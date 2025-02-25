using TrueMoon.Configuration;

namespace TrueMoon;

public interface IAppBuilder
{
    IAppBuilder Configuration(Action<IConfigurationBuilder> action);
    IAppBuilder Setup(Action<IAppConfigurationContext> action);
    IApp Build();
}