namespace TrueMoon.Configuration;

public interface IConfigurationBuilder
{
    void AddProvider(IConfigurationProvider configurationProvider);
    void RemoveProvider<T>(T? configurationProvider = default) where T : IConfigurationProvider;
    IConfiguration Build();
}