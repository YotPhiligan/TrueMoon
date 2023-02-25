using TrueMoon.Configuration;

namespace TrueMoon;

public interface IModule
{
    string Name { get; }
    void Configure(IAppCreationContext context);
    void Execute(IServiceProvider serviceProvider, IConfiguration configuration);
}