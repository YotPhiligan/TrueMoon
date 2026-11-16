using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Configuration;
using TrueMoon.Mithril;

namespace TrueMoon.ManualTests.LaunchProfiles;

// A combined executable consumer of the public launch parser and module entry points.
// Verification inputs are confined to this manual host, never added to product CLI.
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var identity = ConfigurationEnvironment.Capture(NativeRuntimeFactory.BootstrapKey, "-p", UnitParentProcessEventsHandler.ControlPipeKey);
            if (args.Length == 4 && args[0] == "--publish-artifact" && identity.Values.All(value => value == null))
                return ArtifactPreparation.Publish(args[1], args[2], args[3]);
            var options = await AetherLaunchOptions.ParseAsync(args, hasRuntimeBootstrap: identity.Values.Any(value => value != null));
            if (options.Role == AetherLaunchRole.RuntimeChild)
            {
                await AetherEditorHost.RuntimeEntryPoint.RunAsync();
                return 0;
            }
            var provider = ConfigurationEnvironment.Capture("TRUEMOON_LAUNCH_PROVIDER").Values.Single();
            if (provider is not ("cobalt" or "microsoft")) throw new ArgumentException("Select the verification DI provider explicitly.");
            if (options.Role == AetherLaunchRole.Player)
            {
                if (!options.LegacyScene) throw new ArgumentException("This directed fixture requires --scene-format snapshot.");
                return await PlayerProbe.Program.Main([options.ArtifactPath!, options.ReportPath!, provider, options.ScenePath!]);
            }
            await EditorProfile.RunAsync(options, provider == "microsoft");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
