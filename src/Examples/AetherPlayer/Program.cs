using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Configuration;
using TrueMoon.Mithril;

namespace TrueMoon.Examples.AetherPlayer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var identity = ConfigurationEnvironment.Capture(NativeRuntimeFactory.BootstrapKey, "-p", UnitParentProcessEventsHandler.ControlPipeKey);
            var options = await AetherLaunchOptions.ParseAsync(args, AetherLaunchRole.Player, identity.Values.Any(value => value != null));
            if (options.Role != AetherLaunchRole.Player) throw new ArgumentException("This executable supports only the Player role.");
            if (options.Help) { Console.WriteLine(PlayerApplication.Usage); return 0; }
            using var end = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, value) => { value.Cancel = true; end.Cancel(); };
            Console.CancelKeyPress += cancel;
            try { await PlayerApplication.RunAsync(options, end.Token); }
            finally { Console.CancelKeyPress -= cancel; }
            return 0;
        }
        catch (OperationCanceledException) { return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
