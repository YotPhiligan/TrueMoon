using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

public static class CheckpointProbe
{
    public static async Task<object> RunAsync(string directory)
    {
        var factory = new NativeRuntimeFactory(); var authoring = new AuthoringState(); var before = JsonSerializer.Serialize(authoring);
        await using var app = App.Build(c => c.UseAether(o =>
        { o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId; o.ReportDirectory = directory; o.RuntimeFactory = _ => factory; }));
        await app.StartAsync(); var host = app.Services.Resolve<AetherHost>()!; var scene = authoring.Snapshot();
        await host.Supervisor.StartAsync(scene); var firstIdentity = host.Supervisor.Status.Identity!;
        await host.Supervisor.SetPlayingAsync(true);
        await WaitAsync(async () => (await host.Supervisor.GetStatusAsync()).Tick >= 3);
        await host.Supervisor.SetPlayingAsync(false);
        var checkpoint = await host.Supervisor.CaptureCheckpointAsync();
        Check(checkpoint.State.Tick >= 3 && checkpoint.State.Camera.X > 0 && checkpoint.State.Interaction, "Capture did not preserve non-default state.");
        var encoded = GameCheckpointCodec.Encode(checkpoint); Check(encoded.Length < 64 * 1024, "Snapshot exceeded control budget.");
        File.WriteAllBytes(Path.Combine(directory, "checkpoint.bin"), encoded);
        var mismatch = new GameCheckpoint(checkpoint.HostContractId, checkpoint.SourceGameBuildId, "another-scene", scene.Revision, checkpoint.State);
        try { await host.Supervisor.StartAsync(scene, mismatch); throw new InvalidOperationException("Mismatched checkpoint accepted."); }
        catch (InvalidDataException) { }
        Check(host.Supervisor.Status.Identity == firstIdentity && !factory.Leases[0].ResourcesReleased, "Preflight disturbed the old game.");
        var root = FindWorkspace();
        var build = await host.Builds.BuildAsync(new(Path.Combine(root, "ManualTests/Game.Logic/Game.Logic.csproj"), "Game.Logic.dll", "Game.Logic.ProbeGame", scene.Revision));
        Check(build.BuildId != checkpoint.SourceGameBuildId, "Cross-build restore was not exercised.");
        await host.Supervisor.StopAsync();
        var starting = host.Frames.StartAsync(scene, build, checkpoint, 32, 16);
        await WaitAsync(() => Task.FromResult(host.Frames.Display?.Receiver.FirstFrame.IsCompletedSuccessfully == true), starting);
        var display = host.Frames.Display!; ulong firstTick; uint pixel;
        using (var frame = display.Receiver.TakeLatest() ?? throw new IOException("Restored frame missing."))
        {
            firstTick = frame.Metadata.Tick;
            Check(firstTick == checkpoint.State.Tick, "A default-state frame was accepted as restored readiness.");
            pixel = BinaryPrimitives.ReadUInt32LittleEndian(frame.Pixels[(10 * display.Surface.Stride + 16 * 4)..]);
            Check(pixel == FramePattern.Pixel(16, 10, 32, 16, frame.Metadata.FrameId, firstTick, 0xff2080c0), "Restored CPU frame mismatch.");
            host.Frames.Presented(display, frame.Metadata);
        }
        await starting; var restored = await host.Supervisor.CaptureCheckpointAsync();
        Check(restored.State == checkpoint.State && restored.SourceGameBuildId == build.BuildId, "Camera/interaction/tick did not restore across build.");
        var restoredIdentity = host.Supervisor.Status.Identity!;
        Check(restoredIdentity.Generation > firstIdentity.Generation && restoredIdentity.SessionId == firstIdentity.SessionId, "Restore reused old runtime identity.");
        var loaded = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, $"loaded-{restoredIdentity.Generation}.json")));
        Check(loaded.GetProperty("Path").GetString() == Path.Combine(build.DirectoryPath, build.EntryAssembly) && loaded.GetProperty("BuildId").GetString() == build.BuildId,
            "Restore did not use the selected artifact.");
        await host.Supervisor.SetPlayingAsync(true);
        await WaitAsync(async () => unchecked((ulong)(await host.Supervisor.GetStatusAsync()).Tick) > checkpoint.State.Tick);
        await host.Supervisor.SetPlayingAsync(false);
        Check((await host.Supervisor.CaptureCheckpointAsync()).State.Camera.X > checkpoint.State.Camera.X, "Restored game did not continue simulation.");
        await host.Frames.StopAsync();
        await host.Supervisor.StartAsync(scene, "capture-reject"); var rejectedIdentity = host.Supervisor.Status.Identity;
        var explicitResetTick = host.Supervisor.Status.Tick; Check(explicitResetTick == 0, "Start without checkpoint did not explicitly reset.");
        try { await host.Supervisor.CaptureCheckpointAsync(); throw new InvalidOperationException("Expected capture rejection."); }
        catch (CheckpointRejectedException error) { Check(error.Outcome == CheckpointOutcome.CaptureRejected, "Unexpected capture outcome."); }
        Check(host.Supervisor.Status.Identity == rejectedIdentity && host.Supervisor.Status.State == SessionState.Paused && host.Supervisor.Status.Failure == null,
            "Acknowledged capture rejection faulted the old runtime.");
        await host.Supervisor.GetStatusAsync(); await host.Supervisor.SetPlayingAsync(true);
        await WaitAsync(async () => (await host.Supervisor.GetStatusAsync()).Tick > 0); await host.Supervisor.SetPlayingAsync(false); await host.Supervisor.StopAsync();
        try { await host.Supervisor.StartAsync(scene, checkpoint, "restore-reject"); throw new InvalidOperationException("Expected restore rejection."); }
        catch (CheckpointRejectedException) { }
        Check(host.Supervisor.Status.State == SessionState.Faulted && host.Supervisor.Status.Failure?.Phase == "RestoreCheckpoint", "Rejected candidate became ready.");
        await host.Supervisor.StopAsync();
        await host.Supervisor.StartAsync(scene, "capture-timeout");
        try { await host.Supervisor.CaptureCheckpointAsync(); throw new InvalidOperationException("Expected capture timeout."); }
        catch (OperationCanceledException) { }
        Check(host.Supervisor.Status.State == SessionState.Faulted && host.Supervisor.Status.Failure?.Phase == "CaptureCheckpoint", "Unknown capture outcome was treated as recoverable.");
        await host.Supervisor.StopAsync();
        var held = host.Frames.StartAsync(scene, checkpoint, 32, 16, "restore-held");
        await WaitAsync(() => Task.FromResult(host.Supervisor.Status.Identity is { } id && File.Exists(Path.Combine(directory, $"restore-held-{id.Generation}.json"))), held);
        Check(host.Frames.Display == null && host.Frames.MappingCount == 0, "Surface published while restore pending.");
        await host.Frames.StopAsync();
        try { await held; throw new InvalidOperationException("Stop did not cancel restore."); } catch (OperationCanceledException) { }
        await app.StopAsync();
        Check(factory.Leases.All(l => l.Reaped && l.ResourcesReleased) && host.Frames.MappingCount == 0, "Checkpoint workflow leaked native ownership.");
        Check(before == JsonSerializer.Serialize(authoring), "Authoring state changed during restore.");
        Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"), "Editor loaded game types.");
        return new { Passed = true, Scenario = "checkpoint", Checkpoint = checkpoint, Restored = restored, FirstFrameTick = firstTick, Pixel = pixel,
            PayloadBytes = encoded.Length, PayloadSha256 = Convert.ToHexString(SHA256.HashData(encoded)), BuildId = build.BuildId,
            RestoredEntry = loaded, CaptureRejectionResumed = true, UnknownOutcomeFaulted = true, StopDuringRestoreReaped = true, ExplicitResetTick = explicitResetTick,
            EditorPreserved = before == JsonSerializer.Serialize(authoring), GameLoadedInEditor = false, Mappings = host.Frames.MappingCount,
            Leases = factory.Leases.Select(l => new { l.ProcessId, l.Reaped, l.ResourcesReleased }) };
    }
    private static async Task WaitAsync(Func<Task<bool>> condition, Task? pending = null)
    {
        var clock = Stopwatch.StartNew();
        while (!await condition())
        {
            if (pending?.IsFaulted == true || pending?.IsCanceled == true) await pending;
            if (clock.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Checkpoint probe phase timed out.");
            await Task.Delay(10);
        }
    }
    internal static string FindWorkspace()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TrueMoon.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run checkpoint probe from the source workspace.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
