using TrueMoon.Aether.Player;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.Examples.AetherPlayer;

/// <summary>Reusable example composition, also called by the combined executable after role selection.</summary>
public static class PlayerApplication
{
    public const string Usage = "AetherPlayer [--player] --artifact <delivered-build-directory> --scene <scene.aetherscene> [--scene-format world|snapshot] [--width 960] [--height 540] [--title name] [--font font.ttf] [--report directory] [--validation]";

    public static async Task RunAsync(AetherLaunchOptions options, CancellationToken token = default)
    {
        if (options.Role != AetherLaunchRole.Player) throw new ArgumentException("Expected Player role.", nameof(options));
        // Both inputs and all identity checks precede App construction. This path
        // consumes a delivered artifact; it never searches for sources or invokes an SDK.
        var start = await PlayerStart.CaptureAsync(options.ArtifactPath!, BuildIdentity.HostId, options.ScenePath!, options.LegacyScene, token);
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = options.FontPath ?? Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        var report = options.ReportPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrueMoon", "AetherPlayer", "runs", Guid.NewGuid().ToString("N"));
        await using var app = App.Build(context =>
        {
            context.UseDiagnostics(new DiagnosticPipelineOptions { JsonDirectory = Path.Combine(report, "diagnostics"), JsonUseRunDirectories = true });
            context.UseAetherPlayer(start, fonts, player =>
            { player.Width = options.Width; player.Height = options.Height; player.Title = options.Title; player.Validation = options.Validation; });
        });
        try
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(token); startup.CancelAfter(TimeSpan.FromSeconds(30));
            await app.StartAsync(startup.Token);
            var window = app.Services.Resolve<HostedUiWindow>()!; var player = app.Services.Resolve<PlayerWindow>()!;
            var completed = await Task.WhenAny(window.Completion, player.Completion).WaitAsync(token);
            await completed;
        }
        finally { await app.StopAsync(); }
    }
}
