using System.Runtime.InteropServices;
using TrueMoon;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.Examples.AetherEditor;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        const string usage = "AetherEditor --runtime path/to/AetherRuntimeHost.exe [--project project.aetherproj] [--font font.ttf] [--report directory]";
        try
        {
            if (args is ["--help"]) { Console.WriteLine(usage); return 0; }
            if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new PlatformNotSupportedException("This example requires Windows x64 with OpenGL/Vulkan interop.");

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < args.Length; index++)
            {
                var name = args[index];
                if (name is not ("--runtime" or "--project" or "--font" or "--report"))
                    throw new ArgumentException($"Unknown argument '{name}'. {usage}");
                if (++index == args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"{name} requires a value. {usage}");
                if (!values.TryAdd(name, Path.GetFullPath(args[index])))
                    throw new ArgumentException($"Duplicate argument '{name}'.");
            }
            if (!values.TryGetValue("--runtime", out var executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Select the runtime apphost explicitly. {usage}");
            if (!File.Exists(executable)) throw new FileNotFoundException("Runtime apphost not found.", executable);
            var runtimeAssembly = Path.ChangeExtension(executable, ".dll");
            var launch = new NativeRuntimeLaunch(executable, runtimeAssembly);
            var hostId = BuildIdentity.ForRuntime(runtimeAssembly);
            var project = values.GetValueOrDefault("--project");
            if (project != null && !project.EndsWith(".aetherproj", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("--project requires a .aetherproj manifest.");

            var report = values.GetValueOrDefault("--report") ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrueMoon", "AetherEditor", "runs", Guid.NewGuid().ToString("N"));
            var diagnostics = values.ContainsKey("--report") ? Path.Combine(report, "diagnostics", "editor") : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrueMoon", "Diagnostics", "Editor");
            var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
            {
                ["Noto Sans"] = values.GetValueOrDefault("--font") ?? Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf")
            });
            // Main owns composition and paths; all editor services and UI products come from App/DI.
            await using var app = App.Build(context =>
            {
                context.UseDiagnostics(new DiagnosticPipelineOptions { JsonDirectory = diagnostics, JsonUseRunDirectories = true });
                context.UseAetherEditor(engine =>
                {
                    engine.RuntimeLaunch = launch;
                    engine.HostContractId = hostId;
                    engine.GameBuildId = new('0', 64);
                    engine.ReportDirectory = report;
                }, editor => editor.ProjectPath = project);
                context.UseAetherEditorWindow(fonts);
            });
            try
            {
                using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await app.StartAsync(startup.Token);
                await app.Services.Resolve<EditorWindow>()!.Ready.WaitAsync(startup.Token);
                await app.Services.Resolve<HostedUiWindow>()!.Completion;
            }
            finally { await app.StopAsync(); }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
