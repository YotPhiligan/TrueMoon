using System.Diagnostics;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

public sealed class GameBuildTests(GameBuildFixture fixture) : IClassFixture<GameBuildFixture>
{
    [Fact]
    public void PublishedBuilds_LoadDifferentBehaviorAndShareOnlyHostContract()
    {
        var a = fixture.A; var b = fixture.B;
        Assert.NotEqual(a.BuildId, b.BuildId); Assert.Equal(7, a.SourceRevision); Assert.Equal(8, b.SourceRevision);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(a.DirectoryPath, GameBuildArtifact.ManifestFile)))), a.BuildId);
        Assert.Equal(new[] { "Game.Candidate.dll", "Game.Private.dll" }, a.Files);
        using var first = GameBuildLoader.Load(new(a.DirectoryPath, a.BuildId), GameBuildFixture.Hash);
        using var second = GameBuildLoader.Load(new(b.DirectoryPath, b.BuildId), GameBuildFixture.Hash);
        first.Session.Load(new("room", 23, [])); first.Session.Tick();
        Assert.Equal(23, first.Session.Revision); Assert.Equal(1ul, first.Session.TickNumber);
        Assert.Equal(0xff2080c0u, first.Session.PatternColor); Assert.Equal(0xffc06020u, second.Session.PatternColor);
        Assert.Contains(typeof(IGameSession), first.Session.GetType().GetInterfaces());
        Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(first.Session.GetType().Assembly));
        Assert.NotSame(first.Session.GetType().Assembly, second.Session.GetType().Assembly);
        Assert.DoesNotContain(AssemblyLoadContext.Default.Assemblies, assembly => assembly.GetName().Name is "Game.Candidate" or "Game.Private");
        Assert.Equal(Path.Combine(a.DirectoryPath, a.EntryAssembly), first.LoadedAssemblyPath);
    }
    [Fact]
    public void PinnedArtifact_CannotBeOverwrittenAndReleasesEveryPinOnDispose()
    {
        var artifact = fixture.CopyA(); var path = Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly); var bytes = File.ReadAllBytes(path);
        using (var pin = artifact.Pin()) Assert.Throws<IOException>(() => File.WriteAllBytes(path, bytes));
        File.WriteAllBytes(path, bytes);
        Assert.Equal(artifact.BuildId, GameBuildArtifact.Open(artifact.DirectoryPath, GameBuildFixture.Hash).BuildId);
    }
    [Theory]
    [InlineData("manifest", typeof(InvalidDataException))]
    [InlineData("dll", typeof(InvalidDataException))]
    [InlineData("missing", typeof(FileNotFoundException))]
    [InlineData("extra", typeof(InvalidDataException))]
    [InlineData("host", typeof(InvalidDataException))]
    [InlineData("id", typeof(InvalidDataException))]
    public void Open_RejectsTamperedOrMismatchedArtifact(string invalid, Type expected)
    {
        var artifact = fixture.CopyA(); var host = GameBuildFixture.Hash; var id = artifact.BuildId;
        var dll = Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly);
        switch (invalid)
        {
            case "manifest": File.AppendAllText(Path.Combine(artifact.DirectoryPath, GameBuildArtifact.ManifestFile), " "); break;
            case "dll": var bytes = File.ReadAllBytes(dll); bytes[^1] ^= 1; File.WriteAllBytes(dll, bytes); break;
            case "missing": File.Delete(dll); break;
            case "extra": File.WriteAllText(Path.Combine(artifact.DirectoryPath, "undeclared.txt"), "unexpected"); break;
            case "host": host = new('B', 64); break;
            case "id": id = new('B', 64); break;
        }
        Assert.Throws(expected, () => GameBuildArtifact.Open(artifact.DirectoryPath, host, id));
    }
    [Theory]
    [InlineData("schema")]
    [InlineData("contract")]
    [InlineData("path")]
    [InlineData("framework")]
    [InlineData("duplicate")]
    public void RehashedManifest_StillValidatesSchemaContractAndDependencyPolicy(string invalid)
    {
        var artifact = fixture.CopyA(); var manifestPath = Path.Combine(artifact.DirectoryPath, GameBuildArtifact.ManifestFile);
        var json = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        switch (invalid)
        {
            case "schema": json["Schema"] = 2; break;
            case "contract": json["ContractSha256"] = new string('B', 64); break;
            case "path": json["Files"]![0]!["Name"] = "../Game.Candidate.dll"; break;
            case "framework": json["Files"]![0]!["Name"] = "System.Runtime.dll"; break;
            case "duplicate": json["Files"]![1]!["Name"] = "game.candidate.DLL"; break;
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()); File.WriteAllBytes(manifestPath, bytes);
        var id = Convert.ToHexString(SHA256.HashData(bytes)); var renamed = Path.Combine(Path.GetDirectoryName(artifact.DirectoryPath)!, id);
        Directory.Move(artifact.DirectoryPath, renamed);
        Assert.Throws<InvalidDataException>(() => GameBuildArtifact.Open(renamed, GameBuildFixture.Hash, id));
    }
    [Fact]
    public void MissingPrivateDependency_IsRejectedBeforeAnyGameLoad()
    {
        Assert.Throws<InvalidDataException>(() => GameBuildArtifact.Publish(fixture.A.DirectoryPath, Path.Combine(fixture.Root, "incomplete"),
            GameBuildFixture.Hash, fixture.A.EntryAssembly, fixture.A.EntryType, 1));
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(fixture.Root, "incomplete")));
    }
    [Fact]
    public void InvalidEntryType_FailsLoadingAndReleasesArtifactPins()
    {
        var artifact = GameBuildArtifact.Publish(fixture.A.DirectoryPath, Path.Combine(fixture.Root, "wrong-type"), GameBuildFixture.Hash,
            fixture.A.EntryAssembly, "System.String", 1, ["Game.Private.dll"]);
        Assert.Throws<TypeLoadException>(() => GameBuildLoader.Load(new(artifact.DirectoryPath, artifact.BuildId), GameBuildFixture.Hash));
        // Context unload is cooperative. Rename proves our no-delete pin was released
        // without depending on GC to release the CLR's remaining image mapping.
        var path = Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly); File.Move(path, path + ".retired");
        Assert.True(File.Exists(path + ".retired"));
    }
    [Fact]
    public async Task CompilerFailure_PreservesDiagnosticsDoesNotPublishAndCanRetry()
    {
        var root = Path.Combine(fixture.Root, "failed-build");
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, root);
        var project = fixture.Project("broken", "Game.Broken", "this is invalid C#");
        var error = await Assert.ThrowsAsync<GameBuildException>(() => compiler.BuildAsync(new(project, "Game.Broken.dll", "Game.Broken.Game", 41)));
        Assert.NotEqual(0, error.ExitCode); Assert.Null(compiler.ProcessId);
        Assert.Contains("CS", File.ReadAllText(Path.Combine(error.DiagnosticsDirectory, "build.stdout.log")));
        Assert.False(Directory.Exists(Path.Combine(error.DiagnosticsDirectory, "output"))); Assert.False(Directory.Exists(Path.Combine(error.DiagnosticsDirectory, "work")));
        Assert.DoesNotContain(Directory.EnumerateDirectories(root), p => Path.GetFileName(p) != "jobs");
        var retry = await compiler.BuildAsync(new(fixture.ProjectA, fixture.A.EntryAssembly, fixture.A.EntryType, 42, ["Game.Private.dll"]));
        Assert.Equal(42, retry.SourceRevision); Assert.Null(compiler.ProcessId);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppStop_CancelsCompilerReapsGameAndFencesLateJobs(bool di)
    {
        var held = fixture.HeldProject(); var builds = Path.Combine(fixture.Root, "app-" + Guid.NewGuid().ToString("N")); var factory = new LoadedFactory();
        await using var app = App.Builder(c => { if (di) c.UseDI(); }).Setup(c =>
        {
            AetherOptions configured = null!;
            c.UseAether(o => { configured = o; o.HostContractId = o.GameBuildId = GameBuildFixture.Hash; o.ReportDirectory = fixture.Root;
                o.BuildDirectory = builds; o.RuntimeFactory = _ => factory; });
            configured.BuildDirectory = Path.Combine(builds, "changed"); configured.BuildTimeout = TimeSpan.Zero;
        }).Build();
        await app.StartAsync(); var host = Assert.IsType<AetherHost>(app.Services.Resolve<AetherHost>());
        await host.Supervisor.StartAsync(new("room", 1, []), fixture.A);
        var task = host.Builds.BuildAsync(new(held.Project, "Game.Held.dll", "Game.Candidate.Game", 90));
        await TestWait.UntilAsync(() => File.Exists(held.Marker), TimeSpan.FromSeconds(20));
        var childPid = int.Parse(File.ReadAllText(held.Marker)); var compilerPid = host.Builds.ProcessId!.Value;
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Builds.BuildAsync(new(fixture.ProjectA, fixture.A.EntryAssembly, fixture.A.EntryType, 91, ["Game.Private.dll"])));
        await app.StopAsync(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Null(host.Builds.ProcessId); AssertExited(compilerPid); AssertExited(childPid); Assert.Single(factory.Launches);
        Assert.True(Assert.Single(factory.Leases).ResourcesReleased);
        Assert.StartsWith(Path.Combine(builds, "jobs") + Path.DirectorySeparatorChar, host.Builds.DiagnosticsDirectory);
        Assert.Equal(SessionState.Stopped, host.Supervisor.Status.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.Builds.BuildAsync(new(held.Project, "Game.Held.dll", "Game.Candidate.Game", 92)));
        Assert.False(Directory.Exists(Path.Combine(host.Builds.DiagnosticsDirectory!, "work")));
        await app.DisposeAsync();
    }
    [Fact]
    public async Task Compiler_SnapshotsDependenciesAndLeavesCandidateSelectionExplicit()
    {
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, Path.Combine(fixture.Root, "snapshot"));
        var dependencies = new List<string> { "Game.Private.dll" };
        var work = compiler.BuildAsync(new(fixture.ProjectA, fixture.A.EntryAssembly, fixture.A.EntryType, 51, dependencies));
        dependencies.Clear(); dependencies.Add("missing.dll");
        var candidate = await work;
        Assert.Equal(51, candidate.SourceRevision); Assert.Equal(new[] { "Game.Candidate.dll", "Game.Private.dll" }, candidate.Files);
        Assert.Null(compiler.ProcessId);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrDeadline_ReapsCompilerTreeAndRetainsNoCandidate(bool timeout)
    {
        var held = fixture.HeldProject(); var root = Path.Combine(fixture.Root, "cancel-" + Guid.NewGuid().ToString("N"));
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, root, timeout ? TimeSpan.FromSeconds(8) : null);
        using var cancel = new CancellationTokenSource();
        var task = compiler.BuildAsync(new(held.Project, "Game.Held.dll", "Game.Candidate.Game", 3), cancel.Token);
        await TestWait.UntilAsync(() => File.Exists(held.Marker), TimeSpan.FromSeconds(20));
        var child = int.Parse(File.ReadAllText(held.Marker)); var pid = compiler.ProcessId!.Value;
        if (timeout) await Assert.ThrowsAsync<TimeoutException>(() => task);
        else { cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task); }
        Assert.Null(compiler.ProcessId); AssertExited(pid); AssertExited(child);
        Assert.DoesNotContain(Directory.EnumerateDirectories(root), p => Path.GetFileName(p) != "jobs");
        Assert.True(File.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "build.stdout.log")));
    }
    [Fact]
    public async Task ExplicitArtifactStart_ChangesBuildIdentityOnlyOnNewGeneration()
    {
        var factory = new LoadedFactory(); await using var supervisor = new SessionSupervisor(factory, GameBuildFixture.Hash, GameBuildFixture.Hash,
            fixture.Root, new() { HeartbeatInterval = TimeSpan.FromSeconds(30) });
        await supervisor.StartAsync(new("room", 17, []), fixture.A); var a = supervisor.Status.Identity!;
        Assert.Equal(fixture.A.BuildId, a.GameBuildId); Assert.Equal(fixture.A.DirectoryPath, factory.Launches[0].Build!.DirectoryPath);
        Assert.Equal(1, (await supervisor.StepAsync()).Tick);
        await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.StartAsync(new("room", 17, []), fixture.B));
        Assert.Equal(a, supervisor.Status.Identity);
        await supervisor.StopAsync(); await supervisor.StartAsync(new("room", 18, []), fixture.B); var b = supervisor.Status.Identity!;
        Assert.Equal(a.SessionId, b.SessionId); Assert.True(b.Generation > a.Generation); Assert.NotEqual(a.Nonce, b.Nonce);
        Assert.Equal(fixture.B.BuildId, b.GameBuildId); Assert.Equal(18, supervisor.Status.SceneRevision);
        await supervisor.StopAsync(); Assert.All(factory.Leases, lease => Assert.True(lease.ResourcesReleased));
    }
    [Theory]
    [InlineData(0)] [InlineData(121)]
    public void Compiler_RejectsInvalidBudgetBeforeAnyProcess(int seconds)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new GameBuildCompiler(GameBuildFixture.Hash, fixture.Root, TimeSpan.FromSeconds(seconds)));

    private static void AssertExited(int pid)
    {
        try { using var process = Process.GetProcessById(pid); Assert.True(process.HasExited); }
        catch (ArgumentException) { }
    }
    private sealed class LoadedFactory : IRuntimeFactory
    {
        public List<SessionLaunch> Launches { get; } = [];
        public List<LoadedLease> Leases { get; } = [];
        public IRuntimeLease Create(SessionLaunch launch) { Launches.Add(launch); var lease = new LoadedLease(launch); Leases.Add(lease); return lease; }
    }
    private sealed class LoadedLease(SessionLaunch launch) : IRuntimeLease
    {
        private GameBuildLoader _loader = null!; private RuntimeProtocol _protocol = null!;
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ProcessId => 123; public Task Exited => _exit.Task; public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token) { _loader = GameBuildLoader.Load(launch.Build!, launch.Identity.HostContractId); _protocol = new(launch.Identity, _loader.Session, ProcessId); return Task.CompletedTask; }
        public Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token) => _protocol.SendAsync(request, token);
        public Task StopAsync(CancellationToken gracefulDeadline = default) { _loader.Dispose(); _protocol = null!; _exit.TrySetResult(); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { ResourcesReleased = true; return ValueTask.CompletedTask; }
    }
}
