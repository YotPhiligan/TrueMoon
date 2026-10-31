using System.Security;
using TrueMoon.Aether.Builds;
using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

public sealed class GameBuildInputsTests(GameBuildFixture builds) : IClassFixture<GameBuildFixture>
{
    [Fact]
    public async Task TrackedBuild_RetainsStableWitnessAcrossJobsWithoutTrustingReopenedArtifacts()
    {
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, Path.Combine(builds.Root, "stable-artifacts"),
            null, null, new() { MaxJobs = 1 });
        var request = Request("stable", "<PropertyGroup Condition=\"'$(ErrorLog)' != ''\"><DefineConstants>ERRORLOG_PRESENT</DefineConstants></PropertyGroup>" +
            "<ItemGroup><Compile Update=\"Game.cs\"><FingerprintMetadata>" + new string('x', 40000) + "</FingerprintMetadata></Compile></ItemGroup>");
        var first = await compiler.BuildAsync(request);
        var inputs = Assert.IsType<GameBuildInputSnapshot>(first.Inputs);
        using (var query = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(compiler.DiagnosticsDirectory!, "before.evaluation.json"))))
        {
            Assert.Contains("ERRORLOG_PRESENT", query.RootElement.GetProperty("Properties").GetProperty("DefineConstants").GetString());
            Assert.Equal(40000, query.RootElement.GetProperty("Items").GetProperty("Compile")[0].GetProperty("FingerprintMetadata").GetString()!.Length);
        }
        await compiler.VerifyInputsAsync(inputs);
        var second = await compiler.BuildAsync(request with { SourceRevision = 99 });
        Assert.Equal(inputs.Fingerprint, second.Inputs!.Fingerprint);
        Assert.NotEqual(first.BuildId, second.BuildId);
        Assert.Null(GameBuildArtifact.Open(first.DirectoryPath, GameBuildFixture.Hash).Inputs);
        Assert.Contains(inputs.Files, f => f.Path.EndsWith("Game.cs", StringComparison.Ordinal));
        Assert.Null(compiler.ProcessId);
        Assert.All(compiler.ReadDiagnostics(16, 0), item => Assert.True(item.Completed && item.ResourcesReleased));
        Assert.Equal(1, compiler.RetentionStatus!.RetainedJobs); Assert.Equal(0, compiler.RetentionStatus.SkippedDirectories);
        Assert.Equal(0, compiler.RetentionStatus.Failures);
    }

    [Theory] [InlineData("source")] [InlineData("addition")] [InlineData("import")] [InlineData("reference")]
    public async Task PersistentChangeAfterBuild_IsRejectedBySdkReevaluation(string change)
    {
        var referenceDirectory = Path.Combine(builds.Root, "reference-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(referenceDirectory);
        var reference = Path.Combine(referenceDirectory, "Game.Private.dll");
        File.Copy(Path.Combine(builds.A.DirectoryPath, "Game.Private.dll"), reference);
        var import = Path.Combine(builds.Root, "import-" + Guid.NewGuid().ToString("N") + ".props");
        File.WriteAllText(import, "<Project><PropertyGroup><DefineConstants>BEFORE</DefineConstants></PropertyGroup></Project>");
        var project = builds.Project("changed-" + Guid.NewGuid().ToString("N"), "Game.Observed", GameBuildFixture.Source("0xff2080c0u"),
            $"<Reference Include=\"Game.Private\"><HintPath>{SecurityElement.Escape(reference)}</HintPath><Private>true</Private></Reference>",
            $"<Import Project=\"{SecurityElement.Escape(import)}\" />");
        await using var compiler = Compiler();
        var artifact = await compiler.BuildAsync(new(project, "Game.Observed.dll", "Game.Candidate.Game", 1, ["Game.Private.dll"]) { TrackInputs = true });
        var source = Path.Combine(Path.GetDirectoryName(project)!, "Game.cs");
        switch (change)
        {
            case "source": File.AppendAllText(source, "\n// persisted edit"); break;
            case "addition": File.WriteAllText(Path.Combine(Path.GetDirectoryName(project)!, "Added.cs"), "namespace Added; public class Item {}"); break;
            case "import": File.WriteAllText(import, "<Project><PropertyGroup><DefineConstants>AFTER</DefineConstants></PropertyGroup></Project>"); break;
            case "reference":
                var bytes = File.ReadAllBytes(reference); bytes[^1] ^= 1; File.WriteAllBytes(reference, bytes); break;
        }
        var error = await Assert.ThrowsAsync<GameBuildInputsChangedException>(() => compiler.VerifyInputsAsync(artifact.Inputs!));
        Assert.Equal(artifact.Inputs!.Fingerprint, error.Expected); Assert.NotEqual(error.Expected, error.Actual);
        Assert.Null(compiler.ProcessId); Assert.True(Directory.Exists(artifact.DirectoryPath));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ChangeOrCancelDuringCompilation_DrainsOwnedSdkBeforeReturning(bool cancel)
    {
        var controls = Path.Combine(builds.Root, "control-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(controls);
        var marker = Path.Combine(controls, "entered"); var release = Path.Combine(controls, "release"); var script = Path.Combine(controls, "hold.ps1");
        File.WriteAllText(script, "param($marker,$release)\n[IO.File]::WriteAllText($marker,[Environment]::ProcessId.ToString())\nwhile (!(Test-Path -LiteralPath $release)) { Start-Sleep -Milliseconds 20 }\n");
        var command = SecurityElement.Escape($"pwsh -NoProfile -NonInteractive -File \"{script}\" \"{marker}\" \"{release}\"");
        var target = $"<Target Name=\"HoldBuild\" BeforeTargets=\"CoreCompile\"><Exec Command=\"{command}\" /></Target>";
        var request = Request("held", target);
        var artifacts = Path.Combine(controls, "artifacts");
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, artifacts); using var cancellation = new CancellationTokenSource();
        var task = compiler.BuildAsync(request, cancellation.Token);
        try
        {
            await TestWait.UntilAsync(() => File.Exists(marker), TimeSpan.FromSeconds(25));
            var compilerPid = compiler.ProcessId!.Value; var childPid = int.Parse(File.ReadAllText(marker));
            if (cancel) cancellation.Cancel();
            else { File.AppendAllText(Path.Combine(Path.GetDirectoryName(request.ProjectPath)!, "Game.cs"), "\n// changed during compile"); File.WriteAllText(release, "release"); }
            if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            else await Assert.ThrowsAsync<GameBuildInputsChangedException>(() => task);
            Assert.Null(compiler.ProcessId); Assert.True(Exited(compilerPid)); Assert.True(Exited(childPid));
            Assert.DoesNotContain(Directory.EnumerateDirectories(artifacts), p => Path.GetFileName(p).Length == 64);
        }
        finally { File.WriteAllText(release, "release"); }
    }

    private GameBuildCompiler Compiler() => new(GameBuildFixture.Hash, Path.Combine(builds.Root, "observed-artifacts"));
    private GameBuildRequest Request(string name, string target = "")
        => new(builds.Project(name + "-" + Guid.NewGuid().ToString("N"), "Game.Observed", GameBuildFixture.Source("0xff2080c0u"), target: target),
            "Game.Observed.dll", "Game.Candidate.Game", 1) { TrackInputs = true };
    private static bool Exited(int pid) { try { using var p = System.Diagnostics.Process.GetProcessById(pid); return p.HasExited; } catch (ArgumentException) { return true; } }
}
