using System.Text.Json;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

public sealed class GameBuildRetentionIntegrationTests(GameBuildFixture fixture) : IClassFixture<GameBuildFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppBuildRetention_SnapshotsOptionsRetainsLatestFailureAndPreservesPublishedArtifact(bool microsoftDI)
    {
        using var archive = new BuildRetentionTestArchive();
        var retention = new GameBuildRetentionOptions { MaxJobs = 1 };
        var app = App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context =>
        {
            AetherOptions configured = null!;
            context.UseAether(options =>
            {
                configured = options;
                options.HostContractId = GameBuildFixture.Hash; options.GameBuildId = GameBuildFixture.Hash;
                options.ReportDirectory = Path.Combine(archive.TempRoot, "reports"); options.BuildDirectory = archive.Root;
                options.BuildRetention = retention; options.RuntimeFactory = _ => new NeverRuntimeFactory();
            });
            retention.MaxJobs = 100; retention.MaxTotalBytes = long.MaxValue; retention.MaxAge = TimeSpan.FromDays(100);
            configured.BuildRetention = new GameBuildRetentionOptions { MaxJobs = 100 };
        }).Build();
        try
        {
            var compiler = app.Services.Resolve<AetherHost>()!.Builds;
            Assert.Null(compiler.RetentionStatus);
            var result = await compiler.BuildAsync(new(fixture.ProjectA, fixture.A.EntryAssembly, fixture.A.EntryType, 71, ["Game.Private.dll"]));
            var oldJob = compiler.DiagnosticsDirectory!;
            Assert.Equal(1, compiler.RetentionStatus!.RetainedJobs);
            Assert.True(File.Exists(Path.Combine(oldJob, BuildDiagnosticStore.MarkerFile)));
            Assert.True(File.Exists(Path.Combine(oldJob, GameBuildDiagnosticReader.FileName)));
            var project = InvalidProject();
            var error = await Assert.ThrowsAsync<GameBuildException>(() => compiler.BuildAsync(new(project, "Game.Invalid.dll", "Game.Invalid.Entry", 72)));
            var current = compiler.DiagnosticsDirectory!;

            Assert.NotEqual(oldJob, current); Assert.False(Directory.Exists(oldJob)); Assert.True(Directory.Exists(current));
            Assert.Equal(current, error.DiagnosticsDirectory);
            Assert.NotEqual(0, error.ExitCode);
            Assert.True(File.Exists(Path.Combine(result.DirectoryPath, result.EntryAssembly)));
            Assert.Equal(result.BuildId, GameBuildArtifact.Open(result.DirectoryPath, GameBuildFixture.Hash).BuildId);
            var status = compiler.RetentionStatus!;
            Assert.Equal(1, status.RetainedJobs); Assert.Equal(1, status.DeletedJobs);
            Assert.Equal(BuildRetentionTestArchive.Size(current), status.RetainedBytes);
            Assert.Equal(0, status.Failures); Assert.False(status.IsOverBudget); Assert.False(status.MaintenanceBusy);
            Assert.Contains("error CS", compiler.StandardOutputStatus!.Text + compiler.StandardErrorStatus!.Text);
            Assert.False(Directory.Exists(Path.Combine(current, "output"))); Assert.False(Directory.Exists(Path.Combine(current, "work")));
            using var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(current, "build.output.json")));
            Assert.Equal(compiler.StandardOutputStatus, metadata.RootElement.GetProperty("StandardOutput").Deserialize<ProcessOutputSnapshot>());
            Assert.Equal(compiler.StandardErrorStatus, metadata.RootElement.GetProperty("StandardError").Deserialize<ProcessOutputSnapshot>());
            var marker = await File.ReadAllTextAsync(Path.Combine(current, BuildDiagnosticStore.MarkerFile));
            Assert.StartsWith("TrueMoon.Aether.BuildDiagnostics/1\n", marker);
            Assert.DoesNotContain("active", marker);
            Assert.Null(compiler.ProcessId);
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compiler_RetentionDeleteFailureDoesNotReplaceSuccessOrOriginalBuildError(bool compilationFails)
    {
        using var archive = new BuildRetentionTestArchive();
        var retention = new GameBuildRetentionOptions { MaxJobs = 1 };
        var store = archive.Store(retention);
        var blocked = archive.Completed(store, DateTimeOffset.UtcNow.AddHours(-1));
        await using var pipeline = new DiagnosticPipeline(new());
        var configuration = new DiagnosticsConfiguration();
        var sourceName = "RetentionTests." + Guid.NewGuid().ToString("N"); configuration.AddFilter(sourceName);
        using var subscription = new DiagnosticSubscription(configuration, pipeline);
        using var source = new EventsSource(sourceName);
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, archive.Root, null, source, retention);
        string current;
        using (var held = new FileStream(Path.Combine(blocked, "build.stdout.log"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (compilationFails)
            {
                var error = await Assert.ThrowsAsync<GameBuildException>(() => compiler.BuildAsync(new(InvalidProject(), "Game.Invalid.dll", "Game.Invalid.Entry", 73)));
                Assert.NotEqual(0, error.ExitCode);
                Assert.Equal(compiler.DiagnosticsDirectory, error.DiagnosticsDirectory);
                var failure = Assert.Single(pipeline.Read(), record => record.EventName == "Build.Failed");
                Assert.Equal(typeof(GameBuildException).FullName, failure.ExceptionType);
                Assert.Equal(error.Message, failure.ExceptionMessage);
            }
            else
            {
                var result = await compiler.BuildAsync(new(fixture.ProjectA, fixture.A.EntryAssembly, fixture.A.EntryType, 73, ["Game.Private.dll"]));
                Assert.Equal(73, result.SourceRevision);
                Assert.True(File.Exists(Path.Combine(result.DirectoryPath, result.EntryAssembly)));
                Assert.Contains(pipeline.Read(), record => record.EventName == "Build.Completed");
                Assert.DoesNotContain(pipeline.Read(), record => record.EventName == "Build.Failed");
            }
            current = compiler.DiagnosticsDirectory!;
            var status = compiler.RetentionStatus!;
            Assert.Equal(1, status.Failures); Assert.Equal(2, status.RetainedJobs); Assert.True(status.IsOverBudget);
            Assert.InRange(status.LastError!.Length, 1, 1024);
            Assert.True(File.Exists(Path.Combine(blocked, BuildDiagnosticStore.MarkerFile)));
            Assert.True(File.Exists(Path.Combine(current, "build.output.json")));
            Assert.Null(compiler.ProcessId);
            var warning = Assert.Single(pipeline.Read(), record => record.EventName == "Build.RetentionLimited");
            Assert.Equal(1205, warning.EventId); Assert.Equal(DiagnosticLevel.Warning, warning.Level);
            Assert.Equal(Path.GetFileName(current), warning.Properties["JobId"]);
            Assert.Equal("1", warning.Properties["Failures"]); Assert.Equal("True", warning.Properties["IsOverBudget"]);
        }
        var recovered = store.Sweep(DateTimeOffset.UtcNow);
        Assert.Equal(0, recovered.Failures); Assert.Equal(1, recovered.DeletedJobs); Assert.False(recovered.IsOverBudget);
        Assert.False(Directory.Exists(blocked)); Assert.True(Directory.Exists(current));
    }

    private string InvalidProject() => fixture.Project("retention-failure-" + Guid.NewGuid().ToString("N"), "Game.Invalid", "this is invalid C# source");
    private sealed class NeverRuntimeFactory : IRuntimeFactory
    {
        public IRuntimeLease Create(SessionLaunch launch) => throw new InvalidOperationException("Retention tests must not start a runtime.");
    }
}
