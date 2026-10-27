using System.Buffers.Binary;
using System.Text.Json;
using System.Security;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Hosting;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

// ENG3a proof: the Editor compiles bytes; only the owned child loads game types.
public static class BuildProbe
{
    public static async Task<object> RunAsync(string directory)
    {
        var factory = new NativeRuntimeFactory(NativeRuntimeLaunch.CurrentProcess("--runtime")); var authoring = new AuthoringState(); var before = JsonSerializer.Serialize(authoring);
        await using var app = App.Build(c => c.UseAether(o =>
        {
            o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId;
            o.ReportDirectory = directory; o.RuntimeFactory = _ => factory;
        }));
        await app.StartAsync(); var host = app.Services.Resolve<AetherHost>()!; var builds = new List<object>();
        var hostHash = Hash(typeof(AetherHost).Assembly.Location);
        await host.Supervisor.StartAsync(authoring.Snapshot()); var old = host.Supervisor.Status.Identity!;
        var projectA = Project(directory, "a", "0xff2080c0u");
        var a = await host.Builds.BuildAsync(new(projectA, "Game.Candidate.dll", "Game.Candidate.Game", 10));
        Check(host.Supervisor.Status.Identity == old && (await host.Supervisor.GetStatusAsync()).Phase == "Paused", "Compilation changed the active session.");
        var broken = Project(directory, "broken", "invalid C#");
        var failureObserved = false;
        try { await host.Builds.BuildAsync(new(broken, "Game.Candidate.dll", "Game.Candidate.Game", 11)); }
        catch (GameBuildException error) { failureObserved = error.ExitCode != 0 && File.Exists(Path.Combine(error.DiagnosticsDirectory, "build.stdout.log")); }
        Check(failureObserved && host.Supervisor.Status.Identity == old, "Compile failure did not preserve the old runtime.");
        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            try { await host.Builds.BuildAsync(new(projectA, "Game.Candidate.dll", "Game.Candidate.Game", 12), canceled.Token); throw new InvalidOperationException("Build cancellation was ignored."); }
            catch (OperationCanceledException) { }
        }
        Check((await host.Supervisor.StepAsync()).Tick == 1, "Old game stopped responding after failed/canceled build.");
        var projectB = Project(directory, "b", "0xffc06020u");
        var pending = host.Builds.BuildAsync(new(projectB, "Game.Candidate.dll", "Game.Candidate.Game", 13));
        await host.Supervisor.StopAsync(); var b = await pending;
        Check(host.Supervisor.Status.State == SessionState.Stopped && factory.Leases.Count == 1, "Late build completion launched a game after Stop.");
        Check(a.BuildId != b.BuildId && hostHash == Hash(typeof(AetherHost).Assembly.Location), "Build overwrote host or reused A/B identity.");
        var tampered = GameBuildArtifact.Publish(a.DirectoryPath, Path.Combine(directory, "tampered"), a.HostContractId, a.EntryAssembly, a.EntryType, a.SourceRevision);
        var dll = Path.Combine(tampered.DirectoryPath, tampered.EntryAssembly); var bytes = File.ReadAllBytes(dll); bytes[^1] ^= 1; File.WriteAllBytes(dll, bytes);
        try { await host.Supervisor.StartAsync(authoring.Snapshot(), tampered); throw new InvalidOperationException("Tampered build was accepted."); }
        catch (InvalidDataException) { }
        Check(factory.Leases.Count == 1, "Invalid artifact launched an owned child.");
        foreach (var (artifact, color) in new[] { (a, 0xff2080c0u), (b, 0xffc06020u) })
        {
            var start = host.Frames.StartAsync(authoring.Snapshot(), artifact, 32, 16);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (host.Frames.Display?.Receiver.FirstFrame.IsCompletedSuccessfully != true)
            {
                if (start.IsFaulted) await start;
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Candidate did not produce a first frame.");
                await Task.Delay(10);
            }
            var display = host.Frames.Display!; using var frame = display.Receiver.TakeLatest() ?? throw new IOException("Candidate frame missing.");
            const int x = 16, y = 10;
            var pixel = BinaryPrimitives.ReadUInt32LittleEndian(frame.Pixels[(y * display.Surface.Stride + x * 4)..]);
            Check(pixel == FramePattern.Pixel(x, y, 32, 16, frame.Metadata.FrameId, frame.Metadata.Tick, color), "Candidate pixels do not match its game build.");
            host.Frames.Presented(display, frame.Metadata); await start;
            var identity = host.Supervisor.Status.Identity!;
            var report = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, $"loaded-{identity.Generation}.json")));
            var loadedPath = report.GetProperty("Path").GetString()!;
            Check(identity.GameBuildId == artifact.BuildId && report.GetProperty("BuildId").GetString() == artifact.BuildId &&
                loadedPath == Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly) && report.GetProperty("Sha256").GetString() == Hash(loadedPath) &&
                report.GetProperty("PatternColor").GetUInt32() == color, "Child loaded provenance mismatch.");
            try { File.Move(loadedPath, loadedPath + ".retired"); throw new InvalidOperationException("Active build was not pinned."); }
            catch (IOException) { }
            builds.Add(new { artifact.BuildId, artifact.SourceRevision, Path = loadedPath, Sha256 = Hash(loadedPath), Color = color, Pixel = pixel, identity.Generation });
            frame.Dispose(); await host.Frames.StopAsync();
            Check(host.Frames.MappingCount == 0, "Candidate mappings retained after Stop.");
            File.Move(loadedPath, loadedPath + ".retired"); File.Move(loadedPath + ".retired", loadedPath);
        }
        Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name is "Game.Logic" or "Game.Candidate"), "Editor loaded game code.");
        Check(before == JsonSerializer.Serialize(authoring), "Compilation changed editor authoring state.");
        await app.StopAsync();
        Check(host.Builds.ProcessId == null && factory.Leases.All(l => l.Reaped && l.ResourcesReleased), "Compiler/runtime ownership was not released.");
        return new { Passed = true, Scenario = "build-artifacts", Builds = builds, CompilerReaped = host.Builds.ProcessId == null,
            CompileFailurePreservedSession = failureObserved, StaleCompletionDidNotLaunch = true, HostUnchanged = hostHash == Hash(typeof(AetherHost).Assembly.Location),
            EditorPreserved = before == JsonSerializer.Serialize(authoring), GameLoadedInEditor = false, Mappings = host.Frames.MappingCount,
            Leases = factory.Leases.Select(l => new { l.ProcessId, l.Reaped, l.ResourcesReleased }) };
    }
    private static string Project(string directory, string variant, string color)
    {
        var root = Path.Combine(directory, "sources", variant); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
        var path = Path.Combine(root, "Game.Candidate.csproj");
        File.WriteAllText(path, """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            <ItemGroup><Reference Include="TrueMoon.Aether"><HintPath>$(AetherContractPath)</HintPath><Private>false</Private></Reference><Reference Include="TrueMoon.Contracts"><HintPath>$(TrueMoonContractsPath)</HintPath><Private>false</Private></Reference></ItemGroup></Project>
            """);
        var source = color == "invalid C#" ? color : $$"""
            using TrueMoon.Aether.Frames;
            using TrueMoon.Aether.Scenes;
            using TrueMoon.Aether.Sessions;
            namespace Game.Candidate;
            public sealed class Game : IGameSession, TrueMoon.Aether.Runtime.IGameSessionEntry {
                public static IGameSession Create(global::TrueMoon.Services.IServiceResolver? services) => new Game();
                public ulong TickNumber { get; private set; }
                public long Revision { get; private set; }
                public uint PatternColor => {{color}};
                public void Load(SceneSnapshot scene) { Revision=scene.Revision; TickNumber=0; }
                public void Tick() { TickNumber++; }
                public void Dispose() { }
            }
            """;
        File.WriteAllText(Path.Combine(root, "Game.cs"), source); return path;
    }
    internal static (GameBuildRequest Request, string Marker) HeldProject(string directory)
    {
        var project = Project(directory, "close-held", "0xff2080c0u"); var root = Path.GetDirectoryName(project)!;
        var marker = Path.Combine(root, "entered"); var script = Path.Combine(root, "hold.ps1");
        File.WriteAllText(script, "param($marker)\n[IO.File]::WriteAllText($marker,[Environment]::ProcessId.ToString())\nwhile ($true) { Start-Sleep -Milliseconds 100 }\n");
        var command = SecurityElement.Escape($"pwsh -NoProfile -NonInteractive -File \"{script}\" \"{marker}\"");
        File.WriteAllText(project, File.ReadAllText(project).Replace("</Project>", $"<Target Name=\"HoldBuild\" BeforeTargets=\"CoreCompile\"><Exec Command=\"{command}\" /></Target></Project>"));
        return (new(project, "Game.Candidate.dll", "Game.Candidate.Game", 99), marker);
    }
    internal static bool HasExited(int pid)
    {
        try { using var process = System.Diagnostics.Process.GetProcessById(pid); return process.HasExited; }
        catch (ArgumentException) { return true; }
    }
    private static string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
