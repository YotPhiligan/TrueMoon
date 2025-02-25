using TrueMoon.Configuration;

namespace TrueMoon.Modules;

public interface IModule
{
    ModuleExecutionFlowOrder ExecutionFlowOrder { get; }
    string Name { get; }
    void Configure(IAppConfigurationContext context);
    void Execute(IServiceProvider serviceProvider, IConfiguration configuration);
}