using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

public static class ReplacementProbe
{
    public static async Task<object> RunAsync(string directory)
    {
        var factory = new NativeRuntimeFactory(NativeRuntimeLaunch.CurrentProcess("--runtime")); var authoring = new AuthoringState(); var before = JsonSerializer.Serialize(authoring); var maxLive = 0;
        await using var app = App.Build(c => c.UseAether(o =>
        {
            o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId; o.ReportDirectory = directory; o.RuntimeFactory = _ => factory;
            o.StatusChanged = _ => maxLive = Math.Max(maxLive, factory.Leases.Count(l => !l.ResourcesReleased));
        }));
        await app.StartAsync(); var host = app.Services.Resolve<AetherHost>()!; var scene = authoring.Snapshot();
        var root = CheckpointProbe.FindWorkspace(); var source = File.ReadAllText(Path.Combine(root, "ManualTests/Game.Logic/ProbeGame.cs"));
        var a = await host.Builds.BuildAsync(new(Path.Combine(root, "ManualTests/Game.Logic/Game.Logic.csproj"), "Game.Logic.dll", "Game.Logic.ProbeGame", 7));
        var bSource = source.Replace("0xff2080c0", "0xffc06020").Replace("X + 0.125f", "X + 0.25f");
        var b = await BuildAsync(host, directory, "b", bSource, 8);
        var rejected = await BuildAsync(host, directory, "reject", bSource.Replace("state.Validate();", "state.Validate(); if (state.Tick > 0) throw new CheckpointRejectedException(CheckpointOutcome.RestoreRejected);"), 9);
        var marker = Path.Combine(directory, "restore-entered");
        var held = await BuildAsync(host, directory, "held", bSource.Replace("state.Validate();", "state.Validate(); if (state.Tick > 0) { File.WriteAllText(" + JsonSerializer.Serialize(marker) + ", Environment.ProcessId.ToString()); Thread.Sleep(Timeout.Infinite); }"), 10);
        var first = host.Frames.StartAsync(scene, a, 32, 16); await PresentAsync(host.Frames, first, 0xff2080c0); await first;
        for (var i = 0; i < 5; i++) await host.Supervisor.StepAsync();
        var state = await host.Supervisor.CaptureCheckpointAsync(); var old = host.Supervisor.Status.Identity!;
        var replacing = host.Replacement.ReplaceAsync(new(b, old, scene, 8));
        var firstFrame = await PresentAsync(host.Frames, replacing, 0xffc06020, state.State.Tick, old.Generation + 1); var applied = await replacing;
        Check((await host.Supervisor.CaptureCheckpointAsync()).State == state.State && applied.Checkpoint.State == state.State, "Replacement lost complete checkpoint.");
        Check(applied.Identity.GameBuildId == b.BuildId && applied.Identity.Generation == old.Generation + 1 && applied.Identity.Nonce != old.Nonce, "Replacement did not select a fresh build generation.");
        var loaded = Loaded(directory, applied.Identity.Generation, b); Check(maxLive == 1, "Replacement overlapped owned children.");
        await host.Supervisor.SetPlayingAsync(true);
        var minimumGeneration = host.Supervisor.Status.Identity!.Generation + 1;
        var failing = host.Replacement.ReplaceAsync(new(rejected, host.Supervisor.Status.Identity!, scene, 9));
        await PresentAsync(host.Frames, failing, 0xffc06020, minimumGeneration: minimumGeneration);
        GameReplacementException candidateError;
        try { await failing; throw new InvalidOperationException("Rejected candidate succeeded."); }
        catch (GameReplacementException error) { candidateError = error; }
        Check(candidateError.RollbackAttempted && candidateError.RollbackSucceeded && candidateError.InnerException is CheckpointRejectedException && candidateError.RecoveryError == null,
            "Candidate error was lost or rollback failed.");
        Check(host.Supervisor.Status.State == SessionState.Playing && host.Supervisor.Status.Identity!.GameBuildId == b.BuildId, "Rollback did not restore the old build/mode.");
        Loaded(directory, host.Supervisor.Status.Identity!.Generation, b);
        await host.Supervisor.SetPlayingAsync(false);
        var pending = host.Replacement.ReplaceAsync(new(held, host.Supervisor.Status.Identity!, scene, 10));
        await WaitAsync(() => File.Exists(marker), pending); Check(host.Frames.Display == null, "Held candidate published a surface before restore.");
        await host.Frames.StopAsync();
        try { await pending; throw new InvalidOperationException("Stop did not fence replacement."); }
        catch (GameReplacementException error) { Check(!error.RollbackAttempted && !error.RollbackSucceeded, "Stop relaunched old game."); }
        Check(factory.Leases.Count == 5 && host.Frames.MappingCount == 0 && host.Supervisor.Status.State == SessionState.Stopped, "Stop retained ownership or launched late child.");
        var broken = host.Frames.StartAsync(scene, rejected, 32, 16); await PresentAsync(host.Frames, broken, 0xffc06020); await broken;
        for (var i = 0; i < 3; i++) await host.Supervisor.StepAsync();
        var doubleFailure = host.Replacement.ReplaceAsync(new(rejected, host.Supervisor.Status.Identity!, scene, 9));
        GameReplacementException rollbackError;
        try { await doubleFailure; throw new InvalidOperationException("Rollback rejection succeeded."); }
        catch (GameReplacementException error) { rollbackError = error; }
        Check(rollbackError.RollbackAttempted && !rollbackError.RollbackSucceeded && rollbackError.InnerException is CheckpointRejectedException &&
            rollbackError.RecoveryError is CheckpointRejectedException && factory.Leases.Count == 8 && host.Frames.MappingCount == 0 && host.Supervisor.Status.State == SessionState.Faulted,
            "Double failure lost original error, retried rollback or leaked ownership.");
        await app.StopAsync(); Check(factory.Leases.All(l => l.Reaped && l.ResourcesReleased), "Native replacement child leaked.");
        Check(before == JsonSerializer.Serialize(authoring) && !AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"), "Editor state or CLR was changed.");
        return new { Passed = true, Scenario = "replacement", Applied = applied, State = state, FirstFrame = firstFrame, Loaded = loaded,
            Builds = new[] { a, b, rejected, held }.Select(x => new { x.BuildId, x.DirectoryPath, x.EntryAssembly, x.SourceRevision }), MaxLive = maxLive,
            CandidateFailure = new { candidateError.Phase, candidateError.RollbackAttempted, candidateError.RollbackSucceeded, Cause = candidateError.InnerException!.GetType().Name },
            RollbackFailure = new { rollbackError.Phase, rollbackError.RollbackAttempted, rollbackError.RollbackSucceeded, Cause = rollbackError.InnerException!.GetType().Name, Recovery = rollbackError.RecoveryError!.GetType().Name },
            StopFenced = true, EditorPreserved = before == JsonSerializer.Serialize(authoring), GameLoadedInEditor = false, Mappings = host.Frames.MappingCount,
            Leases = factory.Leases.Select(l => new { l.ProcessId, l.Reaped, l.ResourcesReleased }) };
    }
    private static async Task<GameBuildArtifact> BuildAsync(AetherHost host, string directory, string name, string source, long revision)
    {
        var input = Path.Combine(directory, "input-" + name); Directory.CreateDirectory(input);
        File.WriteAllText(Path.Combine(input, "Game.cs"), source);
        var project = Path.Combine(input, "Game.Logic.csproj");
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
            <ItemGroup><Reference Include="TrueMoon.Aether"><HintPath>$(AetherContractPath)</HintPath><Private>false</Private></Reference></ItemGroup></Project>
            """);
        return await host.Builds.BuildAsync(new(project, "Game.Logic.dll", "Game.Logic.ProbeGame", revision));
    }
    private static JsonElement Loaded(string directory, ulong generation, GameBuildArtifact build)
    {
        var loaded = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, $"loaded-{generation}.json")));
        Check(loaded.GetProperty("BuildId").GetString() == build.BuildId && loaded.GetProperty("Path").GetString() == Path.Combine(build.DirectoryPath, build.EntryAssembly) &&
            loaded.GetProperty("Sha256").GetString() == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(build.DirectoryPath, build.EntryAssembly)))), "Selected DLL evidence mismatch.");
        return loaded;
    }
    private static async Task<object> PresentAsync(FrameSession frames, Task pending, uint color, ulong? expectedTick = null, ulong minimumGeneration = 0)
    {
        var clock = Stopwatch.StartNew(); object evidence = new { };
        while (!pending.IsCompleted)
        {
            var display = frames.Display;
            if (display != null && display.Surface.Identity.Generation < minimumGeneration) display = null;
            using var frame = display?.Receiver.TakeLatest();
            if (frame != null)
            {
                var pixel = BinaryPrimitives.ReadUInt32LittleEndian(frame.Pixels[(10 * display!.Surface.Stride + 16 * 4)..]);
                Check(pixel == FramePattern.Pixel(16, 10, 32, 16, frame.Metadata.FrameId, frame.Metadata.Tick, color), "New game behavior/CPU pixels not visible.");
                if (expectedTick is { } tick) Check(frame.Metadata.Tick == tick, "First restored tick mismatch.");
                evidence = new { frame.Metadata.Tick, frame.Metadata.FrameId, Pixel = pixel, Color = color }; frames.Presented(display, frame.Metadata);
            }
            if (clock.Elapsed > TimeSpan.FromSeconds(25)) throw new TimeoutException("Replacement CPU presentation timed out.");
            await Task.Delay(2);
        }
        return evidence;
    }
    private static async Task WaitAsync(Func<bool> condition, Task pending)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (pending.IsCompleted) await pending;
            if (clock.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException("Replacement phase timed out.");
            await Task.Delay(10);
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
