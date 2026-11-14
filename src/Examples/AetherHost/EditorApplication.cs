using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.Examples.AetherHost;

internal static class EditorApplication
{
    internal static async Task RunAsync(AetherLaunchOptions options, CancellationToken token)
    {
        var launch = options.RuntimeHostPath is {} executable
            ? new NativeRuntimeLaunch(executable, Path.ChangeExtension(executable, ".dll"), ["--runtime"])
            : NativeRuntimeLaunch.CurrentProcess("--runtime");
        var hostId = BuildIdentity.ForRuntime(launch.RuntimeAssemblyPath);
        var report = options.ReportPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrueMoon", "AetherHost", "runs", Guid.NewGuid().ToString("N"));
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = options.FontPath ?? Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        await using var app = App.Build(context =>
        {
            context.UseDiagnostics(new DiagnosticPipelineOptions { JsonDirectory = Path.Combine(report, "diagnostics", "editor"), JsonUseRunDirectories = true });
            context.UseAetherEditor(engine =>
            { engine.RuntimeLaunch = launch; engine.HostContractId = hostId; engine.GameBuildId = new('0', 64); engine.ReportDirectory = report; },
                editor => editor.ProjectPath = options.ProjectPath);
            context.UseAetherEditorWindow(fonts);
        });
        try
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(token); startup.CancelAfter(TimeSpan.FromSeconds(30));
            await app.StartAsync(startup.Token); await app.Services.Resolve<EditorWindow>()!.Ready.WaitAsync(startup.Token);
            await app.Services.Resolve<HostedUiWindow>()!.Completion.WaitAsync(token);
        }
        finally { await app.StopAsync(); }
    }
}
