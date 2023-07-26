using TrueMoon.Configuration;

namespace TrueMoon;

public interface IModule
{
    ModuleExecutionFlowOrder ExecutionFlowOrder { get; }
    string Name { get; }
    void Configure(IAppCreationContext context);
    void Execute(IServiceProvider serviceProvider, IConfiguration configuration);
}

public enum ModuleExecutionFlowOrder : byte
{
    Start = 0,
    Mid = 1,
    End = 2
}