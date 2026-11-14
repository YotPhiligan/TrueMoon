using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Configuration;
using TrueMoon.Examples.AetherPlayer;
using TrueMoon.Mithril;

namespace TrueMoon.Examples.AetherHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var environment = ConfigurationEnvironment.Capture(NativeRuntimeFactory.BootstrapKey, "-p", UnitParentProcessEventsHandler.ControlPipeKey);
            var options = await AetherLaunchOptions.ParseAsync(args, hasRuntimeBootstrap: environment.Values.Any(value => value != null));
            if (options.Help)
            {
                Console.WriteLine("AetherHost [--editor] [--project project.aetherproj] [--runtime-host host.exe] [--font font.ttf] [--report directory]");
                Console.WriteLine(PlayerApplication.Usage.Replace("AetherPlayer", "AetherHost --player"));
                Console.WriteLine("--runtime is internal and requires a validated owned-child bootstrap."); return 0;
            }
            // The switch precedes construction of any App or service graph. Protected
            // child identity cannot fall back to a user role even when malformed.
            if (options.Role == AetherLaunchRole.RuntimeChild)
            { await RuntimeApplication.RunAsync(RuntimeBootstrap.ReadEnvironment(), options.ConsoleDiagnostics); return 0; }
            using var end = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, value) => { value.Cancel = true; end.Cancel(); };
            Console.CancelKeyPress += cancel;
            try
            {
                if (options.Role == AetherLaunchRole.Player) await PlayerApplication.RunAsync(options, end.Token);
                else await EditorApplication.RunAsync(options, end.Token);
            }
            finally { Console.CancelKeyPress -= cancel; }
            return 0;
        }
        catch (OperationCanceledException) { return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
