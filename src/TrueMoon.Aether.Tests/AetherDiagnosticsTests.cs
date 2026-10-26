using System.Globalization;
using System.Text.Json;
using Game.Logic;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Builds;
using TrueMoon.Aether.Transport;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests;

public sealed class AetherDiagnosticsTests(GameBuildFixture fixture) : IClassFixture<GameBuildFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionTransitions_AreCorrelatedAndPreserveConfiguredObserver(bool microsoftDI)
    {
        var factory = new RuntimeFactory(); var observations = new List<SessionStatus>();
        await using var app = CreateApp(microsoftDI, factory, observations);
        var host = app.Services.Resolve<AetherHost>()!;
        var pipeline = app.Services.Resolve<DiagnosticPipeline>()!;
        await host.Supervisor.StartAsync(new SceneSnapshot("room", 7, []));
        var first = host.Supervisor.Status.Identity!;
        await host.Supervisor.StepAsync();
        await host.Supervisor.SetPlayingAsync(true);
        await host.Supervisor.StopAsync();
        await host.Supervisor.StartAsync(new SceneSnapshot("room", 8, []));
        var second = host.Supervisor.Status.Identity!;
        await host.Supervisor.StopAsync();

        var records = SessionRecords(pipeline, first.SessionId);
        Assert.Equal(observations.Select(status => status.State.ToString()), records.Select(record => record.Properties["State"]));
        Assert.Equal(observations.Select(status => status.Tick.ToString(CultureInfo.InvariantCulture)), records.Select(record => record.Properties["Tick"]));
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal(1ul, first.Generation);
        Assert.Equal(2ul, second.Generation);
        Assert.NotEqual(first.Nonce, second.Nonce);
        Assert.Contains(records, record => record.Properties["State"] == "Playing" && record.Properties["Tick"] == "1"
            && record.Properties["SceneRevision"] == "7" && record.Properties["ProcessId"] == "123");
        Assert.Contains(records, record => record.Properties["Generation"] == "2" && record.Properties["SceneRevision"] == "8");
        Assert.Equal("Stopped", records[^1].Properties["State"]);
        Assert.All(records, record =>
        {
            Assert.Equal(1000, record.EventId);
            Assert.Equal(DiagnosticLevel.Information, record.Level);
            Assert.Equal(typeof(SessionSupervisor).FullName, record.Source);
            Assert.Equal(pipeline.ApplicationId, record.ApplicationId);
            Assert.Equal(GameBuildFixture.Hash, record.Properties["GameBuildId"]);
            Assert.DoesNotContain("Nonce", record.Properties.Keys);
            Assert.DoesNotContain(first.Nonce, record.Properties.Values);
            Assert.DoesNotContain(second.Nonce, record.Properties.Values);
        });
        Assert.All(factory.Leases, lease => Assert.True(lease.ResourcesReleased));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionLaunchFailure_PreservesOriginalCauseAndPublishesErrorWithSessionIdentity(bool microsoftDI)
    {
        var error = new IOException("owned runtime launch failed");
        var factory = new RuntimeFactory { LaunchError = error };
        var observations = new List<SessionStatus>();
        await using var app = CreateApp(microsoftDI, factory, observations);
        var supervisor = app.Services.Resolve<AetherHost>()!.Supervisor;
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => supervisor.StartAsync(new SceneSnapshot("room", 7, []))));

        var lease = Assert.Single(factory.Leases);
        var records = SessionRecords(app.Services.Resolve<DiagnosticPipeline>()!, lease.Launch.Identity.SessionId);
        var failure = Assert.Single(records, record => record.Properties["State"] == "Faulted");
        Assert.Equal(DiagnosticLevel.Error, failure.Level);
        Assert.Equal("Launch", failure.Properties["Phase"]);
        Assert.Equal("1", failure.Properties["Generation"]);
        Assert.Null(failure.Properties["ProcessId"]);
        Assert.Equal(typeof(IOException).FullName, failure.ExceptionType);
        Assert.Equal("owned runtime launch failed", failure.ExceptionMessage);
        Assert.Same(error, supervisor.Status.Failure!.Cause);
        Assert.Same(error, Assert.Single(observations, status => status.State == SessionState.Faulted).Failure!.Cause);
        Assert.Equal(observations.Count, records.Length);
        Assert.True(lease.ResourcesReleased);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppCompiler_ReportsSuccessfulJobAndPersistsDrainedOutputStatus(bool microsoftDI)
    {
        await using var app = CreateApp(microsoftDI, new RuntimeFactory(), []);
        var compiler = app.Services.Resolve<AetherHost>()!.Builds;
        var result = await compiler.BuildAsync(new(fixture.ProjectA, fixture.A.EntryAssembly, fixture.A.EntryType, 61, ["Game.Private.dll"]));
        var records = BuildRecords(app.Services.Resolve<DiagnosticPipeline>()!, compiler.DiagnosticsDirectory!);

        Assert.Equal(61, result.SourceRevision);
        Assert.Equal(new[] { "Build.Starting", "Build.Completed" }, records.Select(record => record.EventName));
        Assert.Equal(new[] { 1200, 1201 }, records.Select(record => record.EventId));
        Assert.All(records, record => Assert.Equal(DiagnosticLevel.Information, record.Level));
        AssertJobProperties(records, compiler.DiagnosticsDirectory!, "61");
        await AssertOutputMetadataAsync(compiler);
        Assert.Null(compiler.ProcessId);
    }

    [Fact]
    public async Task AppCompiler_ReportsCompilerFailureAndRetainsTextAndOutputMetadata()
    {
        var project = fixture.Project("diagnostic-failure-" + Guid.NewGuid().ToString("N"), "Game.Invalid", "this is invalid C# source");
        await using var app = CreateApp(false, new RuntimeFactory(), []);
        var compiler = app.Services.Resolve<AetherHost>()!.Builds;
        var error = await Assert.ThrowsAsync<GameBuildException>(() => compiler.BuildAsync(new(project, "Game.Invalid.dll", "Game.Invalid.Entry", 62)));
        var records = BuildRecords(app.Services.Resolve<DiagnosticPipeline>()!, compiler.DiagnosticsDirectory!);

        Assert.Equal(new[] { "Build.Starting", "Build.Failed" }, records.Select(record => record.EventName));
        Assert.Equal(new[] { 1200, 1203 }, records.Select(record => record.EventId));
        Assert.Equal(DiagnosticLevel.Error, records[1].Level);
        Assert.Equal(typeof(GameBuildException).FullName, records[1].ExceptionType);
        Assert.Equal(error.Message, records[1].ExceptionMessage);
        AssertJobProperties(records, compiler.DiagnosticsDirectory!, "62");
        await AssertOutputMetadataAsync(compiler);
        Assert.Contains("error CS", compiler.StandardOutputStatus!.Text + compiler.StandardErrorStatus!.Text);
        Assert.Null(compiler.ProcessId);
        Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "work")));
        Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "output")));
    }

    private IApp CreateApp(bool microsoftDI, RuntimeFactory factory, List<SessionStatus> observations)
        => App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context => context
            .UseDiagnostics(new DiagnosticPipelineOptions { MemoryCapacity = 4096, MemoryByteCapacity = 8 * 1024 * 1024 })
            .UseAether(options =>
            {
                options.HostContractId = GameBuildFixture.Hash; options.GameBuildId = GameBuildFixture.Hash;
                options.ReportDirectory = Path.Combine(fixture.Root, "diagnostics-" + Guid.NewGuid().ToString("N"));
                options.RuntimeFactory = _ => factory;
                options.Deadlines = new() { HeartbeatInterval = TimeSpan.FromSeconds(30) };
                options.StatusChanged = observations.Add;
            })).Build();

    private static DiagnosticRecord[] SessionRecords(DiagnosticPipeline pipeline, Guid sessionId)
        => pipeline.Read().Where(record => record.EventName == "Session.StateChanged"
            && record.Properties.GetValueOrDefault("SessionId") == sessionId.ToString("D")).ToArray();

    private static DiagnosticRecord[] BuildRecords(DiagnosticPipeline pipeline, string directory)
        => pipeline.Read().Where(record => record.Source == typeof(GameBuildCompiler).FullName
            && record.Properties.GetValueOrDefault("JobId") == Path.GetFileName(directory)).ToArray();

    private static void AssertJobProperties(IEnumerable<DiagnosticRecord> records, string directory, string revision)
    {
        var id = Path.GetFileName(directory);
        Assert.True(Guid.TryParseExact(id, "N", out _));
        Assert.All(records, record =>
        {
            Assert.Equal(id, record.Properties["JobId"]);
            Assert.Equal(revision, record.Properties["SourceRevision"]);
            Assert.DoesNotContain("Nonce", record.Properties.Keys);
        });
    }

    private static async Task AssertOutputMetadataAsync(GameBuildCompiler compiler)
    {
        var stdout = Assert.IsType<ProcessOutputSnapshot>(compiler.StandardOutputStatus);
        var stderr = Assert.IsType<ProcessOutputSnapshot>(compiler.StandardErrorStatus);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(compiler.DiagnosticsDirectory!, "build.output.json")));
        Assert.Equal(stdout, json.RootElement.GetProperty("StandardOutput").Deserialize<ProcessOutputSnapshot>());
        Assert.Equal(stderr, json.RootElement.GetProperty("StandardError").Deserialize<ProcessOutputSnapshot>());
        foreach (var (name, status) in new[] { ("build.stdout.log", stdout), ("build.stderr.log", stderr) })
        {
            var path = Path.Combine(compiler.DiagnosticsDirectory!, name);
            Assert.InRange(new FileInfo(path).Length, 0, 1024 * 1024);
            Assert.Equal(new FileInfo(path).Length, status.FileBytesWritten);
            Assert.InRange(status.Text.Length, 0, 32 * 1024);
            Assert.Equal(status.TotalCharacters - status.Text.Length, status.DroppedCharacters);
            Assert.Null(status.FileError);
        }
    }

    private sealed class RuntimeFactory : IRuntimeFactory
    {
        internal IOException? LaunchError;
        internal readonly List<RuntimeLease> Leases = [];
        public IRuntimeLease Create(SessionLaunch launch)
        {
            var lease = new RuntimeLease(launch, LaunchError); Leases.Add(lease); return lease;
        }
    }

    private sealed class RuntimeLease(SessionLaunch launch, IOException? launchError) : IRuntimeLease
    {
        private readonly ProbeGame _game = new();
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private RuntimeProtocol? _protocol;
        internal SessionLaunch Launch => launch;
        public int ProcessId => 123;
        public Task Exited => _exit.Task;
        public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token)
        {
            if (launchError is not null) return Task.FromException(launchError);
            _protocol = new(launch.Identity, _game, ProcessId); return Task.CompletedTask;
        }
        public Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token) => _protocol!.SendAsync(request, token);
        public Task StopAsync(CancellationToken gracefulDeadline = default) { _exit.TrySetResult(); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { _game.Dispose(); ResourcesReleased = true; return ValueTask.CompletedTask; }
    }
}
