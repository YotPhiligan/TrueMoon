using System.Diagnostics;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitGameEntry_InjectsBorrowedRuntimeServiceIntoCallerOwnedSessions(bool di)
    {
        var artifact = await BuildFactoryGame("injected-" + di, "new Game(services!.Resolve<SceneResolutionService>())");
        var app = App.Builder(c => { if (di) c.UseDI(); }).Setup(c => c.UseAether(o =>
        {
            o.HostContractId = o.GameBuildId = GameBuildFixture.Hash;
            o.ReportDirectory = fixture.Root; o.RuntimeFactory = _ => new LoadedFactory();
        })).Build();
        var service = Assert.IsType<SceneResolutionService>(app.Services.Resolve<SceneResolutionService>());
        var factory = app.Services.Resolve<IFactory<GameBuildLoader, GameBuildReference>>()!;
        using var first = factory.Create(new(artifact.DirectoryPath, artifact.BuildId));
        using var second = factory.Create(new(artifact.DirectoryPath, artifact.BuildId));
        var firstSession = first.Session; var secondSession = second.Session;
        Assert.NotSame(firstSession, secondSession);
        Assert.Empty(firstSession.GetType().GetConstructors());
        Assert.Same(service, firstSession.GetType().GetProperty("Services")!.GetValue(firstSession));
        Assert.Same(service, secondSession.GetType().GetProperty("Services")!.GetValue(secondSession));
        firstSession.Load(new("first", 19, [])); secondSession.Load(new("second", 23, []));
        await app.DisposeAsync();
        Assert.Equal(19, firstSession.Revision); Assert.Equal(23, secondSession.Revision);
        firstSession.Tick(); secondSession.Tick();
        Assert.Equal(1ul, firstSession.TickNumber); Assert.Equal(1ul, secondSession.TickNumber);
        first.Dispose(); Assert.Equal(-1, firstSession.Revision); Assert.Equal(23, secondSession.Revision);
        second.Dispose(); Assert.Equal(-1, secondSession.Revision);
        Assert.Throws<ObjectDisposedException>(() => first.Session);
        Assert.Throws<ObjectDisposedException>(() => second.Session);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitGameEntry_PropagatesOriginalInjectedFailureAndReleasesPins(bool di)
    {
        var artifact = await BuildFactoryGame("throwing-" + di, "throw services!.Resolve<Exception>()");
        var expected = new IOException("game entry creation failed");
        await using var app = App.Builder(c => { if (di) c.UseDI(); }).Setup(c =>
        {
            c.Services(services => services.Singleton<Exception>(_ => expected));
            c.UseAether(o => { o.HostContractId = o.GameBuildId = GameBuildFixture.Hash;
                o.ReportDirectory = fixture.Root; o.RuntimeFactory = _ => new LoadedFactory(); });
        }).Build();
        var factory = app.Services.Resolve<IFactory<GameBuildLoader, GameBuildReference>>()!;
        Assert.Same(expected, Assert.Throws<IOException>(() => factory.Create(new(artifact.DirectoryPath, artifact.BuildId))));
        AssertArtifactPinsReleased(artifact);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedGameFactoryFailure_ReleasesPinsAndPreservesTypeAndError(bool nullResult)
    {
        var artifact = fixture.CopyA(); var expected = new IOException("injected game factory failed");
        Type? selected = null; var calls = 0;
        var factory = new Factory<IGameSession, Type>(type =>
        {
            selected = type; calls++;
            if (nullResult) return null!;
            throw expected;
        });
        if (nullResult)
            Assert.Equal("Game session factory returned null.", Assert.Throws<InvalidDataException>(() =>
                GameBuildLoader.Load(new(artifact.DirectoryPath, artifact.BuildId), GameBuildFixture.Hash, factory)).Message);
        else Assert.Same(expected, Assert.Throws<IOException>(() =>
            GameBuildLoader.Load(new(artifact.DirectoryPath, artifact.BuildId), GameBuildFixture.Hash, factory)));
        Assert.Equal(1, calls); Assert.Equal(artifact.EntryType, selected!.FullName);
        Assert.Contains(typeof(IGameSession), selected.GetInterfaces());
        Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(selected.Assembly));
        AssertArtifactPinsReleased(artifact);
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
        var failed = Assert.Single(compiler.ReadDiagnostics(16, 128));
        Assert.Equal(Path.GetFileName(error.DiagnosticsDirectory), failed.JobId); Assert.Equal(41, failed.SourceRevision);
        Assert.True(failed.Completed); Assert.True(failed.ResourcesReleased); Assert.Null(failed.GameBuildId);
        Assert.NotNull(failed.StandardOutput); Assert.InRange(failed.StandardOutput.Output.Text.Length, 1, 128);
        Assert.True(failed.StandardOutput.ReadFinished); Assert.True(failed.StandardOutput.FileFinished);
        var retry = await compiler.BuildAsync(new(fixture.ProjectA, fixture.A.EntryAssembly, fixture.A.EntryType, 42, ["Game.Private.dll"]));
        Assert.Equal(42, retry.SourceRevision); Assert.Null(compiler.ProcessId);
        var history = compiler.ReadDiagnostics(16, 64); Assert.Equal(2, history.Count); Assert.Equal(retry.BuildId, history[0].GameBuildId);
        Assert.Equal(42, history[0].SourceRevision); Assert.Equal(failed.Id, history[1].Id); Assert.NotEqual(history[0].JobId, history[1].JobId);
        Assert.All(history, snapshot => { Assert.True(snapshot.ResourcesReleased); Assert.True(snapshot.StandardOutput!.Output.Text.Length <= 64); });
        Assert.Equal(history[0].Id, Assert.Single(compiler.ReadDiagnostics(1, 0)).Id);
        Assert.Equal("", compiler.ReadDiagnostics(1, 0)[0].StandardOutput!.Output.Text);
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
    public async Task Compiler_DeepOwnedJobsBuildAndLoadSameProjectTwiceWithoutChangingFirstArtifact()
    {
        var root = Path.Combine(fixture.Root, "deep");
        var legacyIntermediate = Path.Combine(root, "jobs", new string('0', 32), "work", "obj", "Game.Candidate", "release", "Game.Candidate.dll");
        root += new string('p', Math.Max(1, 265 - legacyIntermediate.Length));
        var passed = false;
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, root);
        try
        {
            var first = await compiler.BuildAsync(new(fixture.ProjectA, fixture.A.EntryAssembly, fixture.A.EntryType, 101, ["Game.Private.dll"]));
            var firstJob = compiler.DiagnosticsDirectory!;
            using var loadedFirst = GameBuildLoader.Load(new(first.DirectoryPath, first.BuildId), GameBuildFixture.Hash);
            loadedFirst.Session.Load(new("deep-job", 17, [])); loadedFirst.Session.Tick();
            var firstBytes = File.ReadAllBytes(loadedFirst.LoadedAssemblyPath);

            var second = await compiler.BuildAsync(new(fixture.ProjectA, fixture.A.EntryAssembly, fixture.A.EntryType, 102, ["Game.Private.dll"]));
            var secondJob = compiler.DiagnosticsDirectory!;
            using var loadedSecond = GameBuildLoader.Load(new(second.DirectoryPath, second.BuildId), GameBuildFixture.Hash);
            loadedSecond.Session.Load(new("second-job", 18, []));

            Assert.Equal(101, first.SourceRevision); Assert.Equal(102, second.SourceRevision);
            Assert.NotEqual(first.BuildId, second.BuildId); Assert.NotEqual(firstJob, secondJob);
            Assert.NotSame(loadedFirst.Session.GetType().Assembly, loadedSecond.Session.GetType().Assembly);
            Assert.Equal(firstBytes, File.ReadAllBytes(loadedFirst.LoadedAssemblyPath));
            Assert.Equal(17, loadedFirst.Session.Revision); Assert.Equal(1ul, loadedFirst.Session.TickNumber);
            Assert.Equal(18, loadedSecond.Session.Revision); Assert.Equal(0ul, loadedSecond.Session.TickNumber);
            Assert.Equal(0xff2080c0u, loadedFirst.Session.PatternColor); Assert.Equal(0xff2080c0u, loadedSecond.Session.PatternColor);
            Assert.Equal(new[] { "Game.Candidate.dll", "Game.Private.dll" }, second.Files);
            Assert.Null(compiler.ProcessId);
            foreach (var job in new[] { firstJob, secondJob })
            {
                Assert.StartsWith(Path.Combine(root, "jobs") + Path.DirectorySeparatorChar, job);
                Assert.True(File.Exists(Path.Combine(job, "build.output.json")));
                Assert.False(Directory.Exists(Path.Combine(job, "output")));
                Assert.False(Directory.Exists(Path.Combine(job, "work")));
            }
            passed = true;
        }
        finally { if (!passed) fixture.PreserveArtifacts = true; }
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

    private async Task<GameBuildArtifact> BuildFactoryGame(string directory, string creation)
    {
        var source = GameBuildFixture.Source("0xff123456u")
            .Replace("=> new Game();", "=> " + creation + ";")
            .Replace("public ulong TickNumber", """
                private Game(TrueMoon.Aether.Scenes.SceneResolutionService services) { Services = services; }
                public TrueMoon.Aether.Scenes.SceneResolutionService Services { get; }
                public ulong TickNumber
                """);
        var project = fixture.Project(directory, "Game.Candidate", source);
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, Path.Combine(fixture.Root, directory + "-artifacts"));
        return await compiler.BuildAsync(new(project, "Game.Candidate.dll", "Game.Candidate.Game", 1));
    }

    private static void AssertArtifactPinsReleased(GameBuildArtifact artifact)
    {
        var path = Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly);
        File.Move(path, path + ".retired");
        Assert.True(File.Exists(path + ".retired")); Assert.False(File.Exists(path));
    }

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
