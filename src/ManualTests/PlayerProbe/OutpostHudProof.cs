using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Player;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace TrueMoon.ManualTests.PlayerProbe;

internal static class OutpostHudProof
{
    internal static async Task<int> RunAsync(string artifact, string scene, string output)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = timeout.Token;
        Directory.CreateDirectory(output);
        var start = await PlayerStart.CaptureAsync(artifact, BuildIdentity.HostId, scene, token: token);
        const string family = "Noto Sans";
        var fonts = SkiaFontCatalog.Load(family, new Dictionary<string, string>
        {
            [family] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf")
        });
        await using var app = App.Build(context => context.UseAetherPlayer(start, fonts, options =>
        {
            options.Width = 800;
            options.Height = 540;
            options.Title = "TrueMoon.Tales outpost HUD proof";
        }));
        var window = app.Services.Resolve<HostedUiWindow>()!;
        var player = app.Services.Resolve<PlayerWindow>()!;
        var session = app.Services.Resolve<PlayerSession>()!;
        var input = app.Services.Resolve<PlayerInput>()!;
        var frames = new List<object>();
        try
        {
            await app.StartAsync(token);
            await player.Ready.WaitAsync(token);
            await OnFrame(ui => ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20, Button: 0)));
            Check(input.IsCaptured, "Click over the HUD did not reach the game canvas.");
            await Move(145, UiKey.S, UiKey.D);
            await Wait(() => session.Hud?.Prompt == "E · Осмотреть поворот");
            await Capture("lookout");
            await Interact();
            await Wait(() => session.Hud?.Prompt == null && session.Hud?.Journal.Contains("Тень замечена") == true);
            await Capture("observed");

            await Move(145, UiKey.W, UiKey.A);
            await Move(175, UiKey.W, UiKey.D);
            await Move(14, UiKey.W, UiKey.A);
            await Wait(() => session.Hud?.Prompt == "E · Доложить дежурному");
            await Capture("duty");
            await Interact();
            await Wait(() => session.Hud?.Status == "Тракт · проверка" && session.Hud?.Prompt == null);
            await Capture("inspection");
            var generation = session.Presentation!.Surface.Generation;
            window.Resize(1040, 540);
            await Wait(() => session.Presentation?.Surface.Generation > generation && session.Status.RenderViewReady);
            await Capture("resized");
            await Wait(() => session.Hud?.Status == "Тракт · закрыт" &&
                session.Hud?.Journal.Contains("Вызваны Истребители") == true);
            await Capture("hunters");
        }
        finally
        {
            await player.StopAsync();
            window.Close();
            await window.Completion.WaitAsync(TimeSpan.FromSeconds(20));
            await app.StopAsync();
        }
        var diagnostics = session.Surface!.Diagnostics;
        Check(session.Hud == null && session.Surface.IsReleased && diagnostics.OpenHandles == 0 &&
            diagnostics.LiveSlots == 0 && diagnostics.SurfacesCreated == diagnostics.SurfacesReleased,
            "HUD or surface remained after Stop.");
        await File.WriteAllTextAsync(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(new
        {
            Completed = true, start.Artifact.BuildId, Frames = frames, Diagnostics = diagnostics
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;

        async Task Move(int ticks, UiKey first, UiKey second)
        {
            await OnFrame(ui => { Key(ui, first, true); Key(ui, second, true); });
            var until = session.Status.Tick + (ulong)ticks;
            await Wait(() => session.Status.Tick >= until);
            await OnFrame(ui => { Key(ui, first, false); Key(ui, second, false); });
        }

        Task Interact() => OnFrame(ui => { Key(ui, UiKey.E, true); Key(ui, UiKey.E, false); });

        async Task Capture(string name)
        {
            var after = player.CopiedFrames;
            await Wait(() => player.CopiedFrames > after);
            await OnFrame(ui =>
            {
                var frame = ((PlayerView)ui.Root).Canvas.Drawn!;
                Check(frame.Presentation.Surface.Transport == TrueMoon.Aether.Runtime.Frames.ViewportTransportKind.Gpu, "Expected a GPU frame.");
                using var image = ((SkiaOpenGLSurface)ui.Rendering).ReadbackImage();
                using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                var screenshot = Path.Combine(output, name + ".png");
                File.WriteAllBytes(screenshot, png.ToArray());
                frames.Add(new { Name = name, Image = screenshot, image.Width, image.Height, Hud = session.Hud });
            });
        }

        async Task Wait(Func<bool> test)
        {
            while (!test())
            {
                if (player.Completion.IsCompleted) await player.Completion;
                await Task.Delay(5, token);
            }
        }

        async Task OnFrame(Action<UiSession> action)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.FramePresented += Presented;
            try
            {
                window.RequestFrame();
                await completion.Task.WaitAsync(token);
            }
            finally { window.FramePresented -= Presented; }

            void Presented(UiSession ui)
            {
                if (completion.Task.IsCompleted) return;
                try { action(ui); completion.TrySetResult(); }
                catch (Exception error) { completion.TrySetException(error); }
            }
        }
    }

    private static void Key(UiSession ui, UiKey key, bool down) =>
        ui.HandleInput(new UiInput(down ? InputKind.KeyDown : InputKind.KeyUp, Key: key));

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
