using TrueMoon.Aluminum;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.Alloy;

public class AlloyModule : IAlloyModule
{
    private readonly IEventsSource<AlloyModule> _eventsSource;

    public AlloyModule(IEventsSource<AlloyModule> eventsSource)
    {
        _eventsSource = eventsSource;

        Configuration = new PresentationConfiguration();
    }

    public ModuleExecutionFlowOrder ExecutionFlowOrder => ModuleExecutionFlowOrder.End;
    public string Name => nameof(AlloyModule);
    public void Configure(IAppCreationContext context)
    {
        context.AddDependencies(registrationContext => registrationContext
            .AddSingleton<IViewManager>()
        );
        
        if (Configuration.StartupViewType is { IsClass: true, IsAbstract: false })
        {
            context.AddDependencies(registrationContext => registrationContext
                .Add(Configuration.StartupViewType, Configuration.StartupViewType)
            );
        }
        else
        {
            throw new InvalidOperationException($"{nameof(Configuration.StartupViewType)} is not a class or null");
        }
    }

    public void Execute(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        var viewManager = serviceProvider.Resolve<IViewManager>();
        
        //viewManager. 
    }

    public PresentationConfiguration Configuration { get; }
}