using TrueMoon.Dependencies;
using TrueMoon.Exceptions;

namespace TrueMoon;

public static class AppExtensions
{
    public static async Task RunAsync(this IApp app1, CancellationToken cancellationToken = default)
    {
        await using var app = app1;
        
        var lifetimeHandler = app.Services.Resolve<IAppLifetimeHandler>();
        if (lifetimeHandler == null)
        {
            throw new AppCreationException($"{nameof(IAppLifetimeHandler)} is missing");
        }
            
        await app.StartAsync(cancellationToken);
            
        await lifetimeHandler.WaitAsync(cancellationToken);
        
        lifetimeHandler.Stopping();
        
        await app.StopAsync(cancellationToken);

        lifetimeHandler.Stopped();
    }
}