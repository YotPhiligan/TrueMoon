using System.Security;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Reflection;
using System.Reflection.Emit;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Aether.Builds;
using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

[Collection(nameof(GameBuildPreparationCollection))]
public sealed class GameBuildPreparationTests
{
    [Fact]
    public async Task SourceWatch_CapturesOriginalClosureAndAbsentProbesWithoutAncestorTrees()
    {
        using var files = new Files();
        var import = Path.Combine(files.Root, "shared.props"); File.WriteAllText(import, "<Project/>");
        var missing = Path.Combine(files.Root, "optional.props");
        var linked = Path.Combine(files.Root, "linked.cs"); File.WriteAllText(linked, "public class Linked {}");
        var hint = Path.Combine(files.ProjectDirectory, "bin", "explicit.dll"); Directory.CreateDirectory(Path.GetDirectoryName(hint)!);
        File.Copy(typeof(IFactory<>).Assembly.Location, hint);
        var config = Path.Combine(files.Root, ".editorconfig"); File.WriteAllText(config, "root = true\n");
        var ignoredAncestor = Path.Combine(files.Root, "unrelated.cs"); File.WriteAllText(ignoredAncestor, "unrelated");
        File.WriteAllText(Path.Combine(files.Root, "NuGet.Config"), "not consulted");
        File.WriteAllText(Path.Combine(files.Root, "Directory.Packages.props"), "not consulted");
        File.WriteAllText(files.Project, files.Xml($"<Import Project=\"../shared.props\"/><Import Project=\"../optional.props\" Condition=\"'$(Configuration)' == 'Never'\"/>" +
            $"<ItemGroup><Compile Include=\"../linked.cs\"/><Reference Include=\"Explicit\"><HintPath>{SecurityElement.Escape(hint)}</HintPath></Reference></ItemGroup>"));
        GameBuildSourceWatchDescriptor descriptor;
        using (var capture = await files.Capture("descriptor"))
        {
            descriptor = Assert.IsType<GameBuildSourceWatchDescriptor>(capture.Snapshot.SourceWatch);
            Assert.Null(capture.Snapshot.SourceWatchUnavailableReason);
            Assert.Equal(files.Project, descriptor.ProjectPath); Assert.Equal(1, descriptor.Version);
            Assert.Equal(new[] { files.ProjectDirectory }, descriptor.SourceTrees);
            Assert.Equal(capture.Snapshot.Files.OrderBy(file => file.Path.ToUpperInvariant(), StringComparer.Ordinal), descriptor.Files);
            foreach (var path in new[] { import, linked, hint, config, files.Source, files.Project, typeof(IFactory<>).Assembly.Location })
            {
                var input = Assert.Single(descriptor.Files, file => file.Path == path);
                Assert.Equal(new FileInfo(path).Length, input.Length);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), input.Sha256);
            }
            Assert.DoesNotContain(descriptor.Files, file => file.Path == ignoredAncestor || file.Path.StartsWith(files.Artifacts, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(descriptor.Probes, probe => probe.Path == import && probe.Exists);
            Assert.Contains(descriptor.Probes, probe => probe.Path == missing && !probe.Exists);
            for (var directory = new DirectoryInfo(files.ProjectDirectory); directory != null; directory = directory.Parent)
                foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets", ".editorconfig", ".globalconfig", "global.json", "MSBuild.rsp", "Directory.Build.rsp" })
                    Assert.Contains(descriptor.Probes, probe => probe.Path == Path.Combine(directory.FullName, name) && probe.Exists == File.Exists(probe.Path));
            Assert.DoesNotContain(descriptor.Probes, probe => Path.GetFileName(probe.Path) is "NuGet.Config" or "Directory.Packages.props");
            var json = JsonSerializer.Serialize(capture.Snapshot);
            Assert.DoesNotContain("SourceWatch", json); Assert.DoesNotContain("SourceFingerprint", json);
            Assert.Throws<NotSupportedException>(() => ((IList<GameBuildInputFile>)descriptor.Files).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)descriptor.SourceTrees).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)descriptor.TreeFiles).Clear());
            Assert.Contains(files.Source, descriptor.TreeFiles);
            Assert.DoesNotContain(hint, descriptor.TreeFiles); Assert.DoesNotContain(linked, descriptor.TreeFiles);
            Assert.Throws<NotSupportedException>(() => ((IList<GameBuildSourceWatchProbe>)descriptor.Probes).Clear());
        }
        // The descriptor owns data only and remains usable after capture leases have been released.
        File.Delete(linked); Assert.Contains(descriptor.Files, file => file.Path == linked);
    }

    [Theory]
    [InlineData("content", true)] [InlineData("create", true)] [InlineData("rename", true)] [InlineData("delete", true)]
    [InlineData("subtree-file", true)] [InlineData("timestamp", false)] [InlineData("empty-directory", false)]
    [InlineData("excluded", false)] [InlineData("unrelated-ancestor", false)] [InlineData("absent-import", true)]
    [InlineData("Directory.Build.props", true)] [InlineData("Directory.Build.targets", true)]
    [InlineData(".editorconfig", true)] [InlineData(".globalconfig", true)] [InlineData("global.json", true)]
    public async Task SourceWatch_FingerprintTracksBytesMembershipAndProbePresence(string change, bool changesIdentity)
    {
        using var files = new Files();
        File.WriteAllText(files.Project, files.Xml("<Import Project=\"../optional.props\" Condition=\"'$(Configuration)' == 'Never'\"/>"));
        var notes = Path.Combine(files.ProjectDirectory, "notes.txt"); File.WriteAllText(notes, "first");
        using var first = await files.Capture("before");
        switch (change)
        {
            case "content": File.WriteAllText(notes, "other"); break; // Same length, different SHA256.
            case "create": File.WriteAllText(Path.Combine(files.ProjectDirectory, "new.txt"), "new"); break;
            case "rename": File.Move(notes, Path.Combine(files.ProjectDirectory, "renamed.txt")); break;
            case "delete": File.Delete(notes); break;
            case "subtree-file":
                var subtree = Path.Combine(files.ProjectDirectory, "new", "nested"); Directory.CreateDirectory(subtree);
                File.WriteAllText(Path.Combine(subtree, "new.txt"), "new"); break;
            case "timestamp": File.SetLastWriteTimeUtc(notes, DateTime.UtcNow.AddDays(-1)); break;
            case "empty-directory": Directory.CreateDirectory(Path.Combine(files.ProjectDirectory, "empty")); break;
            case "excluded":
                foreach (var name in new[] { "bin", "obj", ".git", ".codex", ".agents" })
                {
                    var directory = Path.Combine(files.ProjectDirectory, name); Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "ignored.txt"), "ignored");
                }
                break;
            case "unrelated-ancestor": File.WriteAllText(Path.Combine(files.Root, "other.txt"), "ignored"); break;
            case "absent-import": File.WriteAllText(Path.Combine(files.Root, "optional.props"), "<Project/>"); break;
            case "Directory.Build.props": case "Directory.Build.targets":
                File.WriteAllText(Path.Combine(files.Root, change), "<Project/>"); break;
            case ".editorconfig": File.WriteAllText(Path.Combine(files.Root, change), "root = true\n"); break;
            case ".globalconfig": File.WriteAllText(Path.Combine(files.Root, change), "is_global = true\n"); break;
            case "global.json": File.WriteAllText(Path.Combine(files.Root, change), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"latestMinor\"}}"); break;
        }
        using var second = await GameBuildPreparation.CaptureAsync(files.Request with { SourceRevision = 999 },
            Path.Combine(files.Artifacts, "different-job"), "after", files.Artifacts, default);
        var before = Assert.IsType<GameBuildSourceWatchDescriptor>(first.Snapshot.SourceWatch);
        var after = Assert.IsType<GameBuildSourceWatchDescriptor>(second.Snapshot.SourceWatch);
        Assert.Equal(changesIdentity, before.SourceFingerprint != after.SourceFingerprint);
        if (change == "absent-import")
        {
            var path = Path.Combine(files.Root, "optional.props");
            Assert.Contains(before.Probes, probe => probe.Path == path && !probe.Exists);
            Assert.Contains(after.Probes, probe => probe.Path == path && probe.Exists);
        }
    }

    [Fact]
    public async Task SourceWatch_UsesCompilerExclusionsButRetainsExplicitInputs()
    {
        using var files = new Files();
        var artifacts = Path.Combine(files.ProjectDirectory, "generated-artifacts"); Directory.CreateDirectory(artifacts);
        File.WriteAllText(Path.Combine(artifacts, "ignored.cs"), "not source");
        var bin = Path.Combine(files.ProjectDirectory, "bin"); Directory.CreateDirectory(bin);
        var linked = Path.Combine(bin, "explicit.cs"); File.WriteAllText(linked, "public class Explicit {}");
        File.WriteAllText(Path.Combine(bin, "ignored.cs"), "not source");
        var caseDirectory = Path.Combine(files.ProjectDirectory, "BIN");
        File.WriteAllText(files.Project, files.Xml("<ItemGroup><Compile Include=\"BIN/explicit.cs\"/></ItemGroup>"));
        using var capture = await GameBuildPreparation.CaptureAsync(files.Request, Path.Combine(artifacts, "job"), "inside", artifacts, default);
        var descriptor = Assert.IsType<GameBuildSourceWatchDescriptor>(capture.Snapshot.SourceWatch);
        Assert.Contains(descriptor.Files, file => StringComparer.OrdinalIgnoreCase.Equals(file.Path, linked));
        Assert.DoesNotContain(descriptor.TreeFiles, path => StringComparer.OrdinalIgnoreCase.Equals(path, linked));
        // Windows aliases BIN and bin, so check the case-sensitive name contract directly as well.
        Assert.False(descriptor.Policy.ExcludesEntry(caseDirectory, true));
        Assert.True(descriptor.Policy.ExcludesEntry(bin, true)); Assert.False(descriptor.Policy.ExcludesEntry(bin, false));
        Assert.True(descriptor.Policy.ExcludesEntry(Path.Combine(artifacts, "job", "new.cs"), false));
        Assert.False(descriptor.Policy.ExcludesEntry(artifacts + "-sibling", true));
        Assert.DoesNotContain(descriptor.Files, file => file.Path.StartsWith(artifacts, StringComparison.OrdinalIgnoreCase) || file.Path.EndsWith("ignored.cs"));
    }

    [Fact]
    public async Task SourceWatch_ProbeBudgetDoesNotInvalidateBuildOrChangeDiagnosticSchema()
    {
        using var files = new Files();
        File.WriteAllText(files.Project, files.Xml(string.Concat(Enumerable.Range(0, GameBuildSourceWatchPolicy.MaximumPaths + 1)
            .Select(index => $"<Import Project=\"../absent/{index}.props\" Condition=\"'$(Configuration)' == 'Never'\"/>"))));
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var artifact = await compiler.BuildAsync(files.Request);
        Assert.Null(artifact.Inputs!.SourceWatch);
        Assert.Contains("probe budget", artifact.Inputs.Preparation!.SourceWatchUnavailableReason);
        Assert.True(File.Exists(Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly)));
        Assert.DoesNotContain("SourceWatch", JsonSerializer.Serialize(artifact.Inputs));
        files.AssertDrained(compiler);
    }

    [Fact]
    public async Task SourceWatch_OccupiedAbsentProbeIsUnavailableRatherThanProvenAbsent()
    {
        using var files = new Files(); Directory.CreateDirectory(Path.Combine(files.Root, "optional.props"));
        File.WriteAllText(files.Project, files.Xml("<Import Project=\"../optional.props\" Condition=\"'$(Configuration)' == 'Never'\"/>"));
        using var capture = await files.Capture("occupied");
        Assert.Null(capture.Snapshot.SourceWatch);
        Assert.Contains("could not establish probe presence", capture.Snapshot.SourceWatchUnavailableReason);
    }

    [Theory] [InlineData("MSBuild.rsp")] [InlineData("Directory.Build.rsp")]
    public async Task SourceWatch_AbsentResponseProbeDoesNotWeakenCompilerRejection(string name)
    {
        using var files = new Files();
        using var before = await files.Capture("before-response");
        var path = Path.Combine(files.Root, name);
        Assert.Contains(before.Snapshot.SourceWatch!.Probes, probe => probe.Path == path && !probe.Exists);
        File.WriteAllText(path, "-property:DefineConstants=AMBIENT");
        await Assert.ThrowsAsync<NotSupportedException>(() => files.Capture("response"));
    }

    [Fact]
    public void Packages_RequirePreparationBeforeJobAdmission()
    {
        using var files = new Files(); using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        Assert.Throws<ArgumentException>(() => { _ = compiler.BuildAsync(files.Request with { PackageDirectory = files.PackageDirectory, CapturePreparation = false }); });
        Assert.Null(compiler.DiagnosticsDirectory);
    }

    [Theory] [InlineData("net11.0")] [InlineData("net10.0")]
    public async Task Packages_BuildLockedTransitiveDllsVerifyChangesAndReuseOnlyVerifiedPayload(string target)
    {
        using var files = new Files();
        files.Package("Leaf", 40, target: target); files.Package("Upper", 2, dependencies: new() { ["Leaf"] = "[1.0.0]" }, target: target);
        files.PackageLock(["Upper"], new() { ["Upper"] = new() { ["Leaf"] = "[1.0.0]" }, ["Leaf"] = new() });
        File.WriteAllText(files.Project, files.Xml("<ItemGroup><PackageReference Include=\"Upper\" Version=\"[1.0.0]\"/></ItemGroup>"));
        File.WriteAllText(files.Source, "public class Original { public static int Value => Upper.Value + Leaf.Value; }");
        var request = files.Request with { PackageDirectory = files.PackageDirectory, Dependencies = ["Upper.dll", "Leaf.dll"], ReuseArtifact = true };
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var first = await compiler.BuildAsync(request); Assert.Equal(42, files.Value(first));
        Assert.Contains(first.Inputs!.Preparation!.Files, file => file.Path == Path.Combine(files.PackageDirectory, "leaf.1.0.0.nupkg"));
        Assert.Contains(first.Inputs.Preparation.Files, file => file.Path == Path.Combine(files.ProjectDirectory, "packages.lock.json"));
        var sourceWatch = Assert.IsType<GameBuildSourceWatchDescriptor>(first.Inputs.SourceWatch);
        Assert.Single(sourceWatch.Files, file => file.Path == Path.Combine(files.PackageDirectory, "leaf.1.0.0.nupkg"));
        Assert.Single(sourceWatch.Files, file => file.Path == Path.Combine(files.ProjectDirectory, "packages.lock.json"));
        Assert.DoesNotContain(sourceWatch.Files, file => file.Path.StartsWith(files.Artifacts, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(first.Compilation!.Files, file => file.Path.EndsWith(Path.Combine("packages", "leaf", "1.0.0", "Leaf.dll")));
        await compiler.VerifyInputsAsync(first.Inputs, first.Compilation); files.AssertDrained(compiler);
        var reused = await compiler.BuildAsync(request with { SourceRevision = 2 }); AssertReuse(compiler, true);
        Assert.Equal(first.Compilation.Fingerprint, reused.Compilation!.Fingerprint); Assert.Equal(42, files.Value(reused));
        files.Package("Leaf", 50, target: target); files.PackageLock(["Upper"], new() { ["Upper"] = new() { ["Leaf"] = "[1.0.0]" }, ["Leaf"] = new() });
        await Assert.ThrowsAsync<GameBuildInputsChangedException>(() => compiler.VerifyInputsAsync(reused.Inputs!, reused.Compilation));
        var changed = await compiler.BuildAsync(request with { SourceRevision = 3 }); AssertReuse(compiler, false);
        Assert.Equal(52, files.Value(changed)); Assert.NotEqual(first.Inputs.Fingerprint, changed.Inputs!.Fingerprint); files.AssertDrained(compiler);
    }

    [Fact]
    public async Task Packages_ConsumesSdkGeneratedLockWithoutChangingOriginalLockOrArchive()
    {
        using var files = new Files(); files.Package("Leaf", 40); files.Package("Upper", 2, dependencies: new() { ["Leaf"] = "[1.0.0]" });
        File.WriteAllText(files.Project, files.Xml("<PropertyGroup><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile><RestoreLockedMode>true</RestoreLockedMode></PropertyGroup><ItemGroup><PackageReference Include=\"Upper\" Version=\"[1.0.0]\"/></ItemGroup>"));
        File.WriteAllText(files.Source, "public class Original { public static int Value => Leaf.Value + Upper.Value; }");
        var config = Path.Combine(files.Root, "NuGet.Config");
        File.WriteAllText(config, "<configuration><packageSources><clear/><add key=\"local\" value=\"" + SecurityElement.Escape(files.PackageDirectory) + "\"/></packageSources></configuration>");
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = files.Root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "restore", files.Project, "--configfile", config, "-property:RestorePackagesWithLockFile=true", "-property:RestoreLockedMode=false", "-property:NuGetAudit=false", "-property:RestorePackagesPath=" + Path.Combine(files.Root, "sdk-cache") }) info.ArgumentList.Add(argument);
        using (var sdk = Process.Start(info)!)
        {
            var stdout = sdk.StandardOutput.ReadToEndAsync(); var stderr = sdk.StandardError.ReadToEndAsync();
            try { await sdk.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
            catch { if (!sdk.HasExited) sdk.Kill(entireProcessTree: true); await sdk.WaitForExitAsync(); throw; }
            Assert.True(sdk.ExitCode == 0, await stdout + await stderr);
        }
        var path = Path.Combine(files.ProjectDirectory, "packages.lock.json"); var before = File.ReadAllBytes(path);
        var archive = Path.Combine(files.PackageDirectory, "leaf.1.0.0.nupkg"); var packageBytes = File.ReadAllBytes(archive);
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var result = await compiler.BuildAsync(files.Request with { PackageDirectory = files.PackageDirectory, Dependencies = ["Leaf.dll", "Upper.dll"] });
        Assert.Equal(42, files.Value(result)); await compiler.VerifyInputsAsync(result.Inputs!, result.Compilation!);
        Assert.Equal(before, File.ReadAllBytes(path)); Assert.Equal(packageBytes, File.ReadAllBytes(archive)); files.AssertDrained(compiler);
    }

    [Fact]
    public async Task Packages_ProjectGraphPropagatesCapturedDllsFromDependencyToRoot()
    {
        using var files = new Files(); files.Package("Leaf", 42, target: "netstandard2.0");
        files.Library("Shared", "public static class Shared { public static int Value => Leaf.Value; }",
            "<ItemGroup><PackageReference Include=\"Leaf\" Version=\"1.0.0\"/></ItemGroup>");
        files.PackageLock(["Leaf"], new() { ["Leaf"] = new() }, Path.Combine(files.Root, "Shared"));
        File.WriteAllText(files.Project, files.Xml("<ItemGroup><ProjectReference Include=\"../Shared/Shared.csproj\"/></ItemGroup>"));
        File.WriteAllText(files.Source, "public class Original { public static int Value => Shared.Value; }");
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var artifact = await compiler.BuildAsync(files.Request with { CaptureProjectReferences = true, PackageDirectory = files.PackageDirectory, Dependencies = ["Shared.dll", "Leaf.dll"] });
        Assert.Equal(42, files.Value(artifact)); Assert.Single(artifact.Compilation!.Projects);
        var sourceWatch = Assert.IsType<GameBuildSourceWatchDescriptor>(artifact.Inputs!.SourceWatch);
        Assert.Equal(new[] { Path.Combine(files.Root, "Shared"), files.ProjectDirectory }.Order(StringComparer.OrdinalIgnoreCase), sourceWatch.SourceTrees);
        Assert.Single(sourceWatch.Files, file => file.Path == Path.Combine(files.PackageDirectory, "leaf.1.0.0.nupkg"));
        await compiler.VerifyInputsAsync(artifact.Inputs!, artifact.Compilation); files.AssertDrained(compiler);
    }

    [Theory]
    [InlineData("optin")] [InlineData("lock")] [InlineData("archive")] [InlineData("hash")]
    [InlineData("version")] [InlineData("floating")] [InlineData("metadata")] [InlineData("condition")]
    [InlineData("edge")] [InlineData("unreachable")] [InlineData("identity")] [InlineData("unresolved")]
    [InlineData("cycle")] [InlineData("limit")] [InlineData("undeclared")] [InlineData("lib/net11.0/Leaf.dll")]
    [InlineData("references")] [InlineData("developmentDependency")]
    [InlineData("build/Run.targets")] [InlineData("analyzers/cs/Run.dll")] [InlineData("runtimes/win-x64/native/Run.dll")]
    [InlineData("contentFiles/any/any/run.cs")] [InlineData("ref/net11.0/Leaf.dll")] [InlineData(".signature.p7s")]
    [InlineData("../escape.dll")] [InlineData("lib/net9.0/Other.dll")] [InlineData("assembly")] [InlineData("budget")]
    public async Task Packages_RejectUnsupportedOrChangedClosureBeforeSdkAndDrain(string problem)
    {
        using var files = new Files();
        var extra = problem.Contains('/') || problem == ".signature.p7s" ? problem : null;
        var edges = problem == "cycle" ? new Dictionary<string, string> { ["Leaf"] = "[1.0.0]" } :
            problem == "unresolved" ? new Dictionary<string, string> { ["Missing"] = "[1.0.0]" } : new();
        files.Package("Leaf", 42, dependencies: edges, extra: extra, identity: problem == "identity" ? "Wrong" : null, invalidAssembly: problem == "assembly", oversized: problem == "budget",
            metadata: problem == "references" ? "<references><reference file=\"Leaf.dll\"/></references>" : problem == "developmentDependency" ? "<developmentDependency>true</developmentDependency>" : "");
        var rows = new Dictionary<string, Dictionary<string, string>> { ["Leaf"] = new() };
        if (problem is "cycle" or "unresolved") rows["Leaf"] = edges;
        if (problem == "edge") rows["Leaf"]["Missing"] = "[1.0.0]";
        if (problem == "unreachable") { files.Package("Extra", 1); rows["Extra"] = new(); }
        if (problem == "limit") for (var index = 0; index < 16; index++) { var id = "Extra" + index; files.Package(id, index); rows[id] = new(); }
        files.PackageLock(["Leaf"], rows);
        var item = "<PackageReference Include=\"Leaf\" Version=\"1.0.0\"/>";
        if (problem == "version") item = item.Replace("1.0.0", "2.0.0");
        if (problem == "floating") item = item.Replace("1.0.0", "1.*");
        if (problem == "metadata") item = item.Replace("/>", " PrivateAssets=\"all\"/>");
        if (problem == "condition") item = item.Replace("/>", " Condition=\"'true'=='true'\"/>");
        File.WriteAllText(files.Project, files.Xml("<ItemGroup>" + item + "</ItemGroup>"));
        if (problem == "lock") File.Delete(Path.Combine(files.ProjectDirectory, "packages.lock.json"));
        if (problem == "archive") File.Delete(Path.Combine(files.PackageDirectory, "leaf.1.0.0.nupkg"));
        if (problem == "hash") File.AppendAllText(Path.Combine(files.PackageDirectory, "leaf.1.0.0.nupkg"), "changed");
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var error = await Record.ExceptionAsync(() => compiler.BuildAsync(files.Request with
            { PackageDirectory = problem == "optin" ? null : files.PackageDirectory, Dependencies = problem == "undeclared" ? [] : ["Leaf.dll", "Extra.dll"] }));
        Assert.NotNull(error); Assert.True(error is NotSupportedException or IOException or InvalidDataException or BadImageFormatException, error.ToString());
        Assert.Empty(Directory.GetFiles(compiler.DiagnosticsDirectory!, "*.stdout.log")); files.AssertDrained(compiler);
        Assert.False(File.Exists(Path.Combine(files.Root, "escape.dll")));
    }

    [Fact]
    public async Task Packages_EditAfterSdkAdmissionFailsWithoutPublishingAndReleasesCopies()
    {
        using var files = new Files(); files.Package("Leaf", 42); files.PackageLock(["Leaf"], new() { ["Leaf"] = new() });
        File.WriteAllText(files.Project, files.Xml("<ItemGroup><PackageReference Include=\"Leaf\" Version=\"1.0.0\"/></ItemGroup>"));
        File.WriteAllText(files.Source, "public class Original { public static int Value => Leaf.Value; }");
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var pending = compiler.BuildAsync(files.Request with { PackageDirectory = files.PackageDirectory, Dependencies = ["Leaf.dll"] });
        await TestWait.UntilAsync(() => compiler.DiagnosticsDirectory != null && File.Exists(Path.Combine(compiler.DiagnosticsDirectory, "before-query.stdout.log")), TimeSpan.FromSeconds(30));
        files.Package("Leaf", 50);
        await Assert.ThrowsAsync<InvalidDataException>(() => pending); files.AssertDrained(compiler);
        Assert.Empty(Directory.GetFiles(files.Artifacts, "build.json", SearchOption.AllDirectories));
    }

    [Fact]
    public void ProjectGraph_RequiresPreparationBeforeJobAdmission()
    {
        using var files = new Files(); using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        Assert.Throws<ArgumentException>(() => { _ = compiler.BuildAsync(files.Request with { CaptureProjectReferences = true, CapturePreparation = false }); });
        Assert.Null(compiler.DiagnosticsDirectory); Assert.Empty(compiler.ReadDiagnostics(1, 0));
    }

    [Fact]
    public async Task ProjectGraph_BuildsCapturedLibraryVerifiesChangesAndReusesOnlyVerifiedPayload()
    {
        using var files = new Files(); var dependency = files.Library("Shared", "public static class Shared { public static int Value => 41; }");
        File.WriteAllText(files.Project, files.Xml("<ItemGroup><ProjectReference Include=\"../Shared/Shared.csproj\"/></ItemGroup>"));
        File.WriteAllText(files.Source, "public class Original { public static int Value => Shared.Value + 1; }");
        var request = files.Request with { CaptureProjectReferences = true, Dependencies = ["Shared.dll"] };
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var first = await compiler.BuildAsync(request);
        Assert.Contains(first.Inputs!.Preparation!.Files, file => file.Path == dependency);
        var project = Assert.Single(first.Compilation!.Projects); Assert.Equal("Shared.dll", project.EntryAssembly);
        Assert.Contains(project.Compilation.Files, file => file.Path == dependency);
        Assert.Contains(first.Compilation.Files, file => file.Path.EndsWith(Path.Combine("graph", "output", "Shared.dll")));
        Assert.Equal(42, files.Value(first)); Assert.True(File.Exists(Path.Combine(first.DirectoryPath, "Shared.dll")));
        files.AssertDrained(compiler);
        var lastJob = compiler.DiagnosticsDirectory;
        Assert.Throws<InvalidOperationException>(() => { _ = compiler.VerifyInputsAsync(first.Inputs); });
        Assert.Equal(lastJob, compiler.DiagnosticsDirectory);
        await compiler.VerifyInputsAsync(first.Inputs, first.Compilation); files.AssertDrained(compiler);
        var reused = await compiler.BuildAsync(request with { SourceRevision = 2, ReuseArtifact = true });
        AssertReuse(compiler, true); Assert.False(File.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "prepare.json")));
        Assert.Empty(Directory.GetFiles(compiler.DiagnosticsDirectory!, "graph-*-build.stdout.log"));
        Assert.Equal(first.Compilation.Fingerprint, reused.Compilation!.Fingerprint); Assert.Equal(42, files.Value(reused));
        File.WriteAllText(dependency, "public static class Shared { public static int Value => 99; }");
        await Assert.ThrowsAsync<GameBuildInputsChangedException>(() => compiler.VerifyInputsAsync(reused.Inputs!, reused.Compilation));
        var changed = await compiler.BuildAsync(request with { SourceRevision = 3, ReuseArtifact = true });
        AssertReuse(compiler, false); Assert.Equal(100, files.Value(changed)); Assert.NotEqual(first.Inputs.Fingerprint, changed.Inputs!.Fingerprint);
        files.AssertDrained(compiler);
    }

    [Theory]
    [InlineData("cycle")] [InlineData("metadata")] [InlineData("condition")] [InlineData("undeclared")]
    [InlineData("duplicate")] [InlineData("limit")] [InlineData("task")] [InlineData("package")] [InlineData("overlap")]
    public async Task ProjectGraph_RejectsUnsupportedClosureBeforeSdkAndDrains(string problem)
    {
        using var files = new Files(); files.Library("Shared", "public class Shared { }");
        var extra = ""; var item = "<ProjectReference Include=\"../Shared/Shared.csproj\"/>";
        var dependencies = new List<string> { "Shared.dll" };
        if (problem == "cycle") File.WriteAllText(Path.Combine(files.Root, "Shared", "Shared.csproj"), files.Xml("<ItemGroup><ProjectReference Include=\"../project/Frozen.csproj\"/></ItemGroup>"));
        if (problem == "metadata") item = "<ProjectReference Include=\"../Shared/Shared.csproj\"><ReferenceOutputAssembly>false</ReferenceOutputAssembly></ProjectReference>";
        if (problem == "condition") item = "<ProjectReference Include=\"../Shared/Shared.csproj\" Condition=\"'true' == 'true'\"/>";
        if (problem == "undeclared") dependencies.Clear();
        if (problem == "overlap")
        {
            var nested = Path.Combine(files.ProjectDirectory, "Nested"); Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "Nested.csproj"), files.Xml());
            File.WriteAllText(Path.Combine(nested, "Nested.cs"), "public class Nested { }");
            item = "<ProjectReference Include=\"Nested/Nested.csproj\"/>"; dependencies = ["Nested.dll"];
        }
        if (problem == "duplicate") { files.Library("Other", "public class Other { }", "<PropertyGroup><AssemblyName>Shared</AssemblyName></PropertyGroup>"); item += "<ProjectReference Include=\"../Other/Other.csproj\"/>"; }
        if (problem == "limit") for (var index = 0; index < 3; index++) { files.Library("Extra" + index, "public class Extra" + index + " { }"); item += $"<ProjectReference Include=\"../Extra{index}/Extra{index}.csproj\"/>"; dependencies.Add("Extra" + index + ".dll"); }
        if (problem == "task") extra = "<Target Name=\"BeforeCompile\"><Exec Command=\"unknown\"/></Target>";
        if (problem == "package") extra = "<ItemGroup><PackageReference Include=\"unknown\" Version=\"1\"/></ItemGroup>";
        if (extra.Length != 0) File.WriteAllText(Path.Combine(files.Root, "Shared", "Shared.csproj"), files.Xml(extra));
        File.WriteAllText(files.Project, files.Xml("<ItemGroup>" + item + "</ItemGroup>"));
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        await Assert.ThrowsAsync<NotSupportedException>(() => compiler.BuildAsync(files.Request with { CaptureProjectReferences = true, Dependencies = dependencies }));
        Assert.Empty(Directory.GetFiles(compiler.DiagnosticsDirectory!, "*.stdout.log")); files.AssertDrained(compiler);
    }

    [Fact]
    public async Task ProjectGraph_DiamondCompilesEachDependencyOnceWithIsolatedIntermediates()
    {
        using var files = new Files();
        files.Library("Leaf", "public static class Leaf { public static int Value => 20; }");
        var references = "<ItemGroup><ProjectReference Include=\"../Leaf/Leaf.csproj\"/></ItemGroup>";
        files.Library("Left", "public static class Left { public static int Value => Leaf.Value; }", references);
        files.Library("Right", "public static class Right { public static int Value => Leaf.Value + 2; }", references);
        File.WriteAllText(files.Project, files.Xml("<ItemGroup><ProjectReference Include=\"../Left/Left.csproj\"/><ProjectReference Include=\"../Right/Right.csproj\"/></ItemGroup>"));
        File.WriteAllText(files.Source, "public class Original { public static int Value => Left.Value + Right.Value; }");
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var request = files.Request with { CaptureProjectReferences = true, Dependencies = ["Leaf.dll", "Left.dll", "Right.dll"] };
        var artifact = await compiler.BuildAsync(request);
        Assert.Equal(new[] { "Leaf.dll", "Left.dll", "Right.dll" }, artifact.Compilation!.Projects.Select(project => project.EntryAssembly));
        Assert.Equal(3, Directory.GetFiles(compiler.DiagnosticsDirectory!, "graph-*-build.stdout.log").Length);
        Assert.Equal(42, files.Value(artifact));
        var descriptor = Assert.IsType<GameBuildSourceWatchDescriptor>(artifact.Inputs!.SourceWatch);
        Assert.Equal(new[] { "Leaf", "Left", "project", "Right" }.Select(name => Path.Combine(files.Root, name)), descriptor.SourceTrees);
        Assert.Equal(descriptor.Files.Count, descriptor.Files.Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(descriptor.Probes.Count, descriptor.Probes.Select(probe => probe.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Single(descriptor.Files, file => file.Path == typeof(IFactory<>).Assembly.Location);
        Assert.InRange(artifact.Compilation.TotalBytes, 1, GameBuildInputSnapshot.MaximumTotalBytes);
        await compiler.VerifyInputsAsync(artifact.Inputs!, artifact.Compilation); files.AssertDrained(compiler);
        var fresh = await compiler.BuildAsync(request with { SourceRevision = 2 });
        Assert.Equal(artifact.Compilation.Fingerprint, fresh.Compilation!.Fingerprint);
        foreach (var name in request.Dependencies.Append(request.EntryAssembly))
            Assert.Equal(File.ReadAllBytes(Path.Combine(artifact.DirectoryPath, name)), File.ReadAllBytes(Path.Combine(fresh.DirectoryPath, name)));
    }

    [Theory] [InlineData("missing")] [InlineData("corrupt")]
    public async Task ProjectGraph_UnavailablePrivatePayloadRebuildsInsteadOfReusing(string change)
    {
        using var files = new Files(); files.Library("Shared", "public static class Shared { public static int Value => 42; }");
        File.WriteAllText(files.Project, files.Xml("<ItemGroup><ProjectReference Include=\"../Shared/Shared.csproj\"/></ItemGroup>"));
        File.WriteAllText(files.Source, "public class Original { public static int Value => Shared.Value; }");
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var request = files.Request with { CaptureProjectReferences = true, Dependencies = ["Shared.dll"], ReuseArtifact = true };
        var first = await compiler.BuildAsync(request); var payload = Path.Combine(first.DirectoryPath, "Shared.dll");
        if (change == "missing") File.Delete(payload); else File.AppendAllText(payload, "corrupt");
        var fresh = await compiler.BuildAsync(request with { SourceRevision = 2 });
        AssertReuse(compiler, false); Assert.Single(Directory.GetFiles(compiler.DiagnosticsDirectory!, "graph-*-build.stdout.log"));
        Assert.Equal(42, files.Value(fresh)); files.AssertDrained(compiler);
    }

    [Fact]
    public async Task ProjectGraph_CompilerFailureReportsDependencySourceAndReleasesWholeGraph()
    {
        using var files = new Files(); var source = files.Library("Shared", "invalid C# dependency");
        File.WriteAllText(files.Project, files.Xml("<ItemGroup><ProjectReference Include=\"../Shared/Shared.csproj\"/></ItemGroup>"));
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        await Assert.ThrowsAsync<GameBuildException>(() => compiler.BuildAsync(files.Request with { CaptureProjectReferences = true, Dependencies = ["Shared.dll"] }));
        Assert.Contains(compiler.ReadDiagnostics(1, 0).Single().CompilerDiagnostics!.Entries, item => item.FilePath == source && item.Line == 1);
        files.AssertDrained(compiler);
    }

    [Fact]
    public async Task ProjectGraph_CancelDuringDependencySdkWorkDrainsBeforeNextAdmission()
    {
        using var files = new Files(); files.Library("Shared", "public class Shared { }");
        File.WriteAllText(files.Project, files.Xml("<ItemGroup><ProjectReference Include=\"../Shared/Shared.csproj\"/></ItemGroup>"));
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        using var cancellation = new CancellationTokenSource();
        var building = compiler.BuildAsync(files.Request with { CaptureProjectReferences = true, Dependencies = ["Shared.dll"] }, cancellation.Token);
        await TestWait.UntilAsync(() => compiler.DiagnosticsDirectory != null && Directory.GetFiles(compiler.DiagnosticsDirectory, "graph-*-query.stdout.log").Length != 0, TimeSpan.FromSeconds(30));
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => building); files.AssertDrained(compiler);
        File.WriteAllText(files.Project, files.Xml());
        await compiler.BuildAsync(files.Request); files.AssertDrained(compiler);
    }

    [Fact]
    public async Task Capture_FreezesImportsLinkedSourcesAndGlobMembershipBeforeEvaluation()
    {
        using var files = new Files();
        var linked = Path.Combine(files.Root, "linked.cs"); File.WriteAllText(linked, "public class Linked { }");
        var import = Path.Combine(files.Root, "shared.props"); File.WriteAllText(import, "<Project><PropertyGroup><DefineConstants>FROZEN</DefineConstants></PropertyGroup></Project>");
        File.WriteAllText(files.Project, files.Xml($"<Import Project=\"{SecurityElement.Escape(import)}\"/><ItemGroup><Compile Include=\"{SecurityElement.Escape(linked)}\"/></ItemGroup>"));
        using var capture = await files.Capture("first");
        File.WriteAllText(files.Source, "invalid replacement"); File.Delete(import); File.Delete(linked);
        File.WriteAllText(Path.Combine(files.ProjectDirectory, "Late.cs"), "invalid late addition");

        Assert.Equal("public class Original { }", File.ReadAllText(capture.CopyPath(files.Source)));
        Assert.Equal("public class Linked { }", File.ReadAllText(capture.CopyPath(linked)));
        Assert.Contains("FROZEN", File.ReadAllText(capture.CopyPath(import)));
        Assert.False(File.Exists(capture.CopyPath(Path.Combine(files.ProjectDirectory, "Late.cs"))));
        Assert.Contains(capture.CopyPath(import), File.ReadAllText(capture.ProjectPath));
        Assert.Equal(import, capture.Original(capture.CopyPath(import)));
        Assert.Contains(capture.Snapshot.Files, file => file.Path == linked);
        var info = capture.BuildInfo(); info.ArgumentList.Add("-target:Compile");
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(process.ExitCode == 0, await output + "\n" + await error);
        }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
        var assembly = Directory.EnumerateFiles(Path.Combine(files.Artifacts, "job", "work"), "Frozen.dll", SearchOption.AllDirectories).First();
        using var stream = File.OpenRead(assembly); using var pe = new PEReader(stream); var metadata = pe.GetMetadataReader();
        var types = metadata.TypeDefinitions.Select(handle => metadata.GetString(metadata.GetTypeDefinition(handle).Name)).ToArray();
        Assert.Contains("Original", types); Assert.Contains("Linked", types); Assert.DoesNotContain("Late", types);
    }

    [Fact]
    public async Task Capture_PinsExecutableRuntimeSdkAndEnvironmentWithStableIdentity()
    {
        using var files = new Files();
        using var first = await files.Capture("first"); using var second = await files.Capture("second");
        var info = first.BuildInfo();
        Assert.True(Path.IsPathFullyQualified(info.FileName)); Assert.EndsWith("dotnet.exe", info.FileName);
        Assert.Equal("exec", info.ArgumentList[0]); Assert.Equal("--fx-version", info.ArgumentList[1]);
        Assert.Equal(first.Snapshot.RuntimeVersion, info.ArgumentList[2]);
        Assert.EndsWith(Path.Combine(first.Snapshot.SdkVersion, "MSBuild.dll"), info.ArgumentList[3]);
        Assert.Equal(first.ProjectPath, info.ArgumentList[4]); Assert.Contains("-noAutoResponse", info.ArgumentList);
        Assert.False(info.Environment.ContainsKey("DOTNET_STARTUP_HOOKS"));
        Assert.False(info.Environment.ContainsKey("MSBuildExtensionsPath"));
        Assert.Equal("false", info.Environment["MSBuildEnableWorkloadResolver"]);
        Assert.StartsWith(files.Artifacts, info.Environment["USERPROFILE"]);
        var compilerInfo = new ProcessStartInfo("dotnet"); compilerInfo.ArgumentList.Add("exec"); compilerInfo.ArgumentList.Add("compiler.dll");
        compilerInfo.Environment["DOTNET_STARTUP_HOOKS"] = "ambient-hook.dll"; compilerInfo.Environment["MSBuildExtensionsPath"] = "ambient-extensions";
        first.ConfigureCompiler(compilerInfo);
        Assert.Equal(info.FileName, compilerInfo.FileName); Assert.Equal(first.Snapshot.RuntimeVersion, compilerInfo.ArgumentList[2]);
        Assert.False(compilerInfo.Environment.ContainsKey("DOTNET_STARTUP_HOOKS")); Assert.False(compilerInfo.Environment.ContainsKey("MSBuildExtensionsPath"));
        Assert.Equal(first.Snapshot.Fingerprint, second.Snapshot.Fingerprint);
        Assert.Equal(first.Snapshot.ToolchainFingerprint, second.Snapshot.ToolchainFingerprint);
        Assert.Equal(first.Snapshot.EnvironmentFingerprint, second.Snapshot.EnvironmentFingerprint);
        Assert.True(first.Snapshot.ElapsedMilliseconds > 0 && first.Snapshot.SourceMilliseconds > 0 && first.Snapshot.ToolchainMilliseconds > 0);
        Assert.True(first.Snapshot.ElapsedMilliseconds >= first.Snapshot.SourceMilliseconds + first.Snapshot.ToolchainMilliseconds);
        Assert.InRange(first.Snapshot.ToolchainFiles, 1, 8192);
        Assert.InRange(first.Snapshot.ToolchainBytes, 1, 1024L * 1024 * 1024);
        Assert.True(first.Snapshot.ToolchainEnumerationMilliseconds > 0 && first.Snapshot.ToolchainOpenMilliseconds > 0 && first.Snapshot.ToolchainHashMilliseconds > 0);
        Assert.True(first.Snapshot.ToolchainMilliseconds >= first.Snapshot.ToolchainEnumerationMilliseconds + first.Snapshot.ToolchainOpenMilliseconds + first.Snapshot.ToolchainHashMilliseconds);
        File.AppendAllText(files.Source, "//next"); using var changed = await files.Capture("changed");
        Assert.NotEqual(first.Snapshot.Fingerprint, changed.Snapshot.Fingerprint);
        Assert.Throws<NotSupportedException>(() => ((IList<GameBuildInputFile>)first.Snapshot.Files).Clear());
        // Independent captures retain independent leases, even after reading to EOF.
        // MSBuild.dll itself can also be held by the test runner's build process.
        var sdkMetadata = Path.Combine(Path.GetDirectoryName(info.ArgumentList[3])!, ".version");
        Assert.Throws<IOException>(() => ReadExclusive(sdkMetadata));
        first.Dispose(); second.Dispose();
        Assert.Throws<IOException>(() => ReadExclusive(sdkMetadata));
        changed.Dispose();
        ReadExclusive(sdkMetadata);

        static void ReadExclusive(string path) { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None); }
    }

    [Theory]
    [InlineData("<Target Name=\"BeforeCompile\"><Exec Command=\"unknown\"/></Target>")]
    [InlineData("<UsingTask TaskName=\"External\" AssemblyFile=\"unknown.dll\"/>")]
    [InlineData("<PropertyGroup><DefineConstants>$([System.IO.File]::ReadAllText('unknown'))</DefineConstants></PropertyGroup>")]
    [InlineData("<PropertyGroup><CustomBeforeMicrosoftCommonTargets>unknown.targets</CustomBeforeMicrosoftCommonTargets></PropertyGroup>")]
    [InlineData("<ItemGroup><PackageReference Include=\"External\" Version=\"1.0\"/></ItemGroup>")]
    [InlineData("<ItemGroup><ProjectReference Include=\"../Other.csproj\"/></ItemGroup>")]
    [InlineData("<ItemGroup><Compile Include=\"../**/*.cs\"/></ItemGroup>")]
    [InlineData("<Import Project=\"unknown.props\" Condition=\"Exists('unknown.props')\"/>")]
    [InlineData("<Import Project=\"unknown.props\" Sdk=\"Unknown.Sdk\"/>")]
    [InlineData("<PropertyGroup><DefineConstants>$(DefineConstants.ToUpper())</DefineConstants></PropertyGroup>")]
    public async Task UnsupportedGraph_IsRejectedBeforeAnySdkProcessAndCleansStaging(string element)
    {
        using var files = new Files(); File.WriteAllText(files.Project, files.Xml(element));
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        await Assert.ThrowsAsync<NotSupportedException>(() => compiler.BuildAsync(files.Request));
        Assert.Null(compiler.ProcessId);
        Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "preparation")));
        Assert.True(compiler.ReadDiagnostics(1, 0).Single().ResourcesReleased);
    }

    [Fact]
    public async Task MissingLinkedInputAndOversizeSource_DoNotProducePartialCapture()
    {
        using var files = new Files();
        File.WriteAllText(files.Project, files.Xml("<ItemGroup><Compile Include=\"missing.cs\"/></ItemGroup>"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => files.Capture("missing"));
        File.WriteAllText(files.Project, files.Xml());
        using (var source = File.OpenWrite(files.Source)) source.SetLength(GameBuildInputSnapshot.MaximumFileBytes + 1);
        await Assert.ThrowsAsync<InvalidDataException>(() => files.Capture("oversize"));
    }

    [Fact]
    public async Task CancelDuringFreeze_ReleasesOwnershipAndRemovesPreparation()
    {
        using var files = new Files(); await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        using var cancellation = new CancellationTokenSource();
        var building = compiler.BuildAsync(files.Request, cancellation.Token);
        await TestWait.UntilAsync(() => compiler.DiagnosticsDirectory != null && Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory, "preparation", "before", "home")), TimeSpan.FromSeconds(20));
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => building);
        Assert.Null(compiler.ProcessId); Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "preparation")));
        Assert.True(compiler.ReadDiagnostics(1, 0).Single().ResourcesReleased);
    }

    [Fact]
    public async Task SdkFrozenBuild_VerifiesOriginalInputsPreservesPathsAndProducesRepeatableBytes()
    {
        using var files = new Files();
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        GameBuildArtifact first;
        try { first = await compiler.BuildAsync(files.Request); }
        catch (Exception error)
        {
            files.Preserve = true;
            var directory = compiler.DiagnosticsDirectory!;
            throw new IOException(error.Message + "\n" + string.Join("\n", Directory.EnumerateFiles(directory).Where(path => path.EndsWith(".log") || path.EndsWith(".inputs.json")).Select(File.ReadAllText)), error);
        }
        var preparation = Assert.IsType<GameBuildPreparationSnapshot>(first.Inputs!.Preparation);
        Assert.Contains(preparation.Files, file => file.Path == files.Source);
        Assert.Contains(first.Compilation!.Files, file => file.Path == files.Source);
        Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "preparation")));
        await compiler.VerifyInputsAsync(first.Inputs, first.Compilation);
        var second = await compiler.BuildAsync(files.Request with { SourceRevision = 2 });
        Assert.Equal(first.Inputs.Fingerprint, second.Inputs!.Fingerprint);
        Assert.Equal(first.Compilation.Fingerprint, second.Compilation!.Fingerprint);
        Assert.Equal(File.ReadAllBytes(Path.Combine(first.DirectoryPath, first.EntryAssembly)), File.ReadAllBytes(Path.Combine(second.DirectoryPath, second.EntryAssembly)));
        File.AppendAllText(files.Source, "//changed");
        await Assert.ThrowsAsync<GameBuildInputsChangedException>(() => compiler.VerifyInputsAsync(first.Inputs, first.Compilation));
        Assert.Null(compiler.ProcessId); Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "preparation")));
        Assert.Equal(0, compiler.RetentionStatus!.SkippedDirectories);
    }

    [Fact]
    public async Task Reuse_RepublishesVerifiedPayloadForNewTicketWithoutCompiler()
    {
        using var files = new Files(); await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var first = await compiler.BuildAsync(files.Request); // An explicit fresh Build can seed a later Start.
        var request = files.Request with { SourceRevision = 2, ReuseArtifact = true };
        var reused = await compiler.BuildAsync(request);
        AssertReuse(compiler, true);
        Assert.NotEqual(first.BuildId, reused.BuildId); Assert.Equal(2, reused.SourceRevision);
        Assert.Equal(first.Inputs!.Fingerprint, reused.Inputs!.Fingerprint);
        Assert.Equal(first.Inputs.SourceWatch!.SourceFingerprint, reused.Inputs.SourceWatch!.SourceFingerprint);
        Assert.NotSame(first.Inputs.SourceWatch, reused.Inputs.SourceWatch);
        Assert.Equal(first.Compilation!.Fingerprint, reused.Compilation!.Fingerprint);
        Assert.Equal(File.ReadAllBytes(Path.Combine(first.DirectoryPath, first.EntryAssembly)), File.ReadAllBytes(Path.Combine(reused.DirectoryPath, reused.EntryAssembly)));
        var reopened = GameBuildArtifact.Open(reused.DirectoryPath, GameBuildFixture.Hash);
        Assert.Equal(2, reopened.SourceRevision); Assert.Null(reopened.Inputs); Assert.Null(reopened.Compilation);
        await compiler.VerifyInputsAsync(reused.Inputs, reused.Compilation);
        var fresh = await compiler.BuildAsync(request with { SourceRevision = 3, ReuseArtifact = false });
        Assert.True(File.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "prepare.json")));
        var sameTicket = await compiler.BuildAsync(request with { SourceRevision = 3 });
        AssertReuse(compiler, true); Assert.Equal(fresh.BuildId, sameTicket.BuildId);
    }

    [Theory] [InlineData("source")] [InlineData("import")] [InlineData("entry")]
    public async Task Reuse_ChangedContentOrEntryRebuilds(string change)
    {
        using var files = new Files();
        var import = Path.Combine(files.Root, "game.props"); File.WriteAllText(import, "<Project><PropertyGroup><DefineConstants>FIRST</DefineConstants></PropertyGroup></Project>");
        File.WriteAllText(files.Project, files.Xml($"<Import Project=\"{SecurityElement.Escape(import)}\"/>"));
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var request = files.Request with { ReuseArtifact = true };
        var first = await compiler.BuildAsync(request);
        if (change == "source") File.AppendAllText(files.Source, "public class Added { }");
        if (change == "import") File.WriteAllText(import, "<Project><PropertyGroup><DefineConstants>SECOND</DefineConstants></PropertyGroup></Project>");
        var next = await compiler.BuildAsync(request with { SourceRevision = 2, EntryType = change == "entry" ? "Another" : request.EntryType });
        AssertReuse(compiler, false); Assert.NotEqual(first.Inputs!.Fingerprint, next.Inputs!.Fingerprint);
        Assert.Equal(2, next.SourceRevision);
    }

    [Theory] [InlineData("deleted")] [InlineData("corrupt")] [InlineData("new-compiler")]
    public async Task Reuse_UnavailablePayloadOrNewCompilerRebuilds(string change)
    {
        using var files = new Files();
        await using var firstCompiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var request = files.Request with { ReuseArtifact = true };
        var first = await firstCompiler.BuildAsync(request);
        if (change == "deleted") GameBuildArtifact.DeleteOwned(first.DirectoryPath, files.Artifacts);
        if (change == "corrupt") File.AppendAllText(Path.Combine(first.DirectoryPath, first.EntryAssembly), "corrupt");
        await using var nextCompiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var compiler = change == "new-compiler" ? nextCompiler : firstCompiler;
        var next = await compiler.BuildAsync(request with { SourceRevision = 2 });
        AssertReuse(compiler, false);
        Assert.Equal(next.BuildId, GameBuildArtifact.Open(next.DirectoryPath, GameBuildFixture.Hash).BuildId);
    }

    [Fact]
    public async Task Reuse_FailedBuildAndStaleVerificationDiscardPreviousResult()
    {
        using var files = new Files(); await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var request = files.Request with { ReuseArtifact = true };
        await compiler.BuildAsync(request);
        var source = File.ReadAllText(files.Source); File.WriteAllText(files.Source, "invalid source");
        await Assert.ThrowsAsync<GameBuildException>(() => compiler.BuildAsync(request with { SourceRevision = 2 }));
        File.WriteAllText(files.Source, source);
        var fresh = await compiler.BuildAsync(request with { SourceRevision = 3 }); AssertReuse(compiler, false);
        File.AppendAllText(files.Source, "//stale");
        await Assert.ThrowsAsync<GameBuildInputsChangedException>(() => compiler.VerifyInputsAsync(fresh.Inputs!, fresh.Compilation!));
        File.WriteAllText(files.Source, source);
        await compiler.BuildAsync(request with { SourceRevision = 4 }); AssertReuse(compiler, false);
    }

    [Fact]
    public async Task Reuse_CancelledProbeDrainsAndDiscardsPreviousResult()
    {
        using var files = new Files(); await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var request = files.Request with { ReuseArtifact = true }; await compiler.BuildAsync(request);
        var oldJob = compiler.DiagnosticsDirectory;
        using var cancellation = new CancellationTokenSource();
        var pending = compiler.BuildAsync(request with { SourceRevision = 2 }, cancellation.Token);
        await TestWait.UntilAsync(() => compiler.DiagnosticsDirectory != null && compiler.DiagnosticsDirectory != oldJob &&
            Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory, "preparation", "before", "home")), TimeSpan.FromSeconds(20));
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Null(compiler.ProcessId); Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "preparation")));
        await compiler.BuildAsync(request with { SourceRevision = 3 }); AssertReuse(compiler, false);
    }

    [Fact]
    public void Reuse_RequiresFrozenPreparationBeforeAdmission()
    {
        using var files = new Files(); using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        Assert.Throws<ArgumentException>(() => { _ = compiler.BuildAsync(files.Request with { CapturePreparation = false, ReuseArtifact = true }); });
        Assert.Null(compiler.DiagnosticsDirectory);
    }

    private static void AssertReuse(GameBuildCompiler compiler, bool reused)
    {
        var directory = compiler.DiagnosticsDirectory!;
        using var evidence = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "reuse.json")));
        Assert.Equal(reused, evidence.RootElement.GetProperty("Reused").GetBoolean());
        Assert.Equal(!reused, File.Exists(Path.Combine(directory, "prepare.json")));
        Assert.Equal(!reused, File.Exists(Path.Combine(directory, "build.stdout.log")));
        Assert.Null(compiler.ProcessId); Assert.False(Directory.Exists(Path.Combine(directory, "preparation")));
        Assert.False(Directory.Exists(Path.Combine(directory, "capture"))); Assert.False(Directory.Exists(Path.Combine(directory, "output")));
        Assert.True(compiler.ReadDiagnostics(1, 0).Single().ResourcesReleased);
        Assert.Equal(0, compiler.RetentionStatus!.SkippedDirectories);
    }

    [Fact]
    public async Task FrozenCompilerFailure_ReportsOriginalLocationAndReleasesAllStaging()
    {
        using var files = new Files(); File.WriteAllText(files.Source, "invalid C# source");
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        await Assert.ThrowsAsync<GameBuildException>(() => compiler.BuildAsync(files.Request));
        Assert.Contains(compiler.ReadDiagnostics(1, 0).Single().CompilerDiagnostics!.Entries,
            item => item.Severity == GameBuildDiagnosticSeverity.Error && item.FilePath == files.Source && item.Line == 1);
        Assert.Null(compiler.ProcessId); Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "preparation")));
        Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "capture")));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task RepositoryGameProject_FreezesAncestorPropsAndContractReferences(bool artifactInsideCheckout)
    {
        using var files = new Files();
        var project = Path.GetFullPath("../../../../ManualTests/Game.Logic/Game.Logic.csproj", AppContext.BaseDirectory);
        var results = Path.GetFullPath("../../TestResults", Path.GetDirectoryName(project)!);
        var artifacts = artifactInsideCheckout ? Path.Combine(results, "RepositoryFrozenBuild", Guid.NewGuid().ToString("N")) : files.Artifacts;
        try
        {
            await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, artifacts);
            var artifact = await compiler.BuildAsync(new(project, "Game.Logic.dll", "Game.Logic.RoomGame", 1)
                { TrackInputs = true, CaptureCompilation = true, CapturePreparation = true });
            Assert.Contains(artifact.Inputs!.Preparation!.Files, file => file.Path == Path.GetFullPath("../../Directory.Build.props", Path.GetDirectoryName(project)!));
            Assert.Contains(artifact.Compilation!.Files, file => file.Path == typeof(TrueMoon.Aether.Sessions.IGameSession).Assembly.Location);
            Assert.Equal("net11.0", GameBuildProfile.TargetFramework);
            Assert.Equal(11, Version.Parse(artifact.Inputs.Preparation.SdkVersion).Major);
            using (var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifact.DirectoryPath, GameBuildArtifact.ManifestFile))))
            {
                Assert.Equal(1, manifest.RootElement.GetProperty("Schema").GetInt32());
                Assert.Equal("net11.0", manifest.RootElement.GetProperty("TargetFramework").GetString());
            }
            using (var evaluation = JsonDocument.Parse(File.ReadAllText(Path.Combine(compiler.DiagnosticsDirectory!, "before.evaluation.json"))))
                Assert.Contains("runtime-async=on", evaluation.RootElement.GetProperty("Properties").GetProperty("Features").GetString());
            using (var preparation = JsonDocument.Parse(File.ReadAllText(Path.Combine(compiler.DiagnosticsDirectory!, "prepare.json"))))
            {
                var arguments = preparation.RootElement.GetProperty("Items").GetProperty("CscCommandLineArgs").EnumerateArray().Select(argument => argument.GetProperty("Identity").GetString()!).ToArray();
                Assert.Contains(arguments, argument => argument.Contains("runtime-async=on", StringComparison.Ordinal));
                Assert.DoesNotContain(arguments, argument => argument.StartsWith("/sourcelink:", StringComparison.OrdinalIgnoreCase));
            }
            Assert.True(File.Exists(Path.Combine(artifact.DirectoryPath, "Game.Logic.dll")));
            Assert.Null(compiler.ProcessId); Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "preparation")));
        }
        finally { if (artifactInsideCheckout) GameBuildArtifact.DeleteOwned(artifacts, results); }
    }

    [Theory]
    [InlineData("10.0.100", "latestMinor", false)]
    [InlineData("12.0.100", "latestMinor", false)]
    [InlineData("11.0.100-preview.1", "latestMinor", false)]
    [InlineData("11.0.100", "latestMajor", false)]
    [InlineData("11.0.100", "latestMinor", true)]
    public async Task FrozenSdk_RejectsUnsupportedSelectionBeforeSdkAndDrains(string version, string policy, bool prerelease)
    {
        using var files = new Files();
        File.WriteAllText(Path.Combine(files.Root, "global.json"), JsonSerializer.Serialize(new
            { sdk = new { version, rollForward = policy, allowPrerelease = prerelease } }));
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        var error = await Assert.ThrowsAsync<NotSupportedException>(() => compiler.BuildAsync(files.Request));
        Assert.Contains("net11.0", error.Message);
        Assert.Contains("SDK11", error.Message);
        Assert.Null(compiler.ProcessId);
        Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "preparation")));
        Assert.DoesNotContain(Directory.EnumerateDirectories(files.Artifacts), path => Path.GetFileName(path).Length == 64);
    }

    [Theory] [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public async Task InvalidFreezeOptions_AreRejectedBeforeJobAdmission(bool tracking, bool compilation)
    {
        using var files = new Files(); await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, files.Artifacts);
        Assert.Throws<ArgumentException>(() => { _ = compiler.BuildAsync(files.Request with { TrackInputs = tracking, CaptureCompilation = compilation }); });
        Assert.Null(compiler.DiagnosticsDirectory); Assert.Empty(compiler.ReadDiagnostics(1, 0));
    }

    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "TrueMoon-Preparation-" + Guid.NewGuid().ToString("N"));
        internal string ProjectDirectory => Path.Combine(Root, "project");
        internal string Project => Path.Combine(ProjectDirectory, "Frozen.csproj");
        internal string Source => Path.Combine(ProjectDirectory, "Original.cs");
        internal string Artifacts => Path.Combine(Root, "artifacts");
        internal string PackageDirectory => Path.Combine(Root, "packages");
        internal GameBuildRequest Request => new(Project, "Frozen.dll", "Original", 1) { TrackInputs = true, CaptureCompilation = true, CapturePreparation = true };
        internal bool Preserve;
        internal Files()
        {
            Directory.CreateDirectory(ProjectDirectory); File.WriteAllText(Project, Xml());
            File.WriteAllText(Source, "public class Original { }");
        }
        internal string Xml(string extra = "") => "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>" + GameBuildProfile.TargetFramework + "</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>" + extra + "</Project>";
        internal string Library(string name, string source, string extra = "")
        {
            var directory = Path.Combine(Root, name); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, name + ".csproj"), Xml(extra));
            var path = Path.Combine(directory, name + ".cs"); File.WriteAllText(path, source); return path;
        }
        internal int Value(GameBuildArtifact artifact)
        {
            var context = new System.Runtime.Loader.AssemblyLoadContext("Graph-" + Guid.NewGuid(), true);
            context.Resolving += (_, name) =>
            { using var stream = File.OpenRead(Path.Combine(artifact.DirectoryPath, name.Name + ".dll")); return context.LoadFromStream(stream); };
            try
            {
                using var stream = File.OpenRead(Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly));
                return (int)context.LoadFromStream(stream).GetType("Original")!.GetProperty("Value")!.GetValue(null)!;
            }
            finally { context.Unload(); }
        }
        internal void AssertDrained(GameBuildCompiler compiler)
        {
            Assert.Null(compiler.ProcessId);
            foreach (var directory in new[] { "preparation", "capture", "graph", "output", "work" }) Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, directory)));
            Assert.True(compiler.ReadDiagnostics(1, 0).Single().ResourcesReleased);
            Assert.Equal(0, compiler.RetentionStatus!.SkippedDirectories);
        }
        internal Task<GameBuildPreparation> Capture(string phase) => GameBuildPreparation.CaptureAsync(Request, Path.Combine(Artifacts, "job"), phase, Artifacts, default);
        internal void Package(string id, int value, Dictionary<string, string>? dependencies = null, string? extra = null,
            string? target = null, string? identity = null, bool invalidAssembly = false, bool oversized = false, string metadata = "")
        {
            target ??= GameBuildProfile.TargetFramework;
            Directory.CreateDirectory(PackageDirectory);
            var path = Path.Combine(PackageDirectory, id.ToLowerInvariant() + ".1.0.0.nupkg");
            using var file = File.Create(path); using var zip = new ZipArchive(file, ZipArchiveMode.Create);
            using (var output = zip.CreateEntry(id + ".nuspec").Open())
            using (var writer = new StreamWriter(output)) writer.Write($"<package><metadata><id>{identity ?? id}</id><version>1.0.0</version><dependencies>" +
                string.Join("", (dependencies ?? []).Select(dependency => $"<dependency id=\"{dependency.Key}\" version=\"{dependency.Value}\"/>")) + "</dependencies>" + metadata + "</metadata></package>");
            using (var output = zip.CreateEntry($"lib/{target}/{id}.dll").Open())
            {
                if (invalidAssembly) output.Write([1, 2, 3]);
                else
                {
                    var assembly = new PersistedAssemblyBuilder(new AssemblyName(id), typeof(object).Assembly);
                    var type = assembly.DefineDynamicModule(id).DefineType(id, TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                    var getter = type.DefineMethod("get_Value", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName, typeof(int), Type.EmptyTypes);
                    var il = getter.GetILGenerator(); il.Emit(OpCodes.Ldc_I4, value); il.Emit(OpCodes.Ret);
                    type.DefineProperty("Value", PropertyAttributes.None, typeof(int), null).SetGetMethod(getter); type.CreateType();
                    using var bytes = new MemoryStream(); assembly.Save(bytes); output.Write(bytes.ToArray());
                }
            }
            if (extra != null) using (var writer = new StreamWriter(zip.CreateEntry(extra).Open())) writer.Write("unsupported");
            if (oversized)
            {
                using var output = zip.CreateEntry("README.txt").Open(); var buffer = new byte[1024 * 1024];
                for (var index = 0; index < 65; index++) output.Write(buffer);
            }
        }
        internal void PackageLock(string[] direct, Dictionary<string, Dictionary<string, string>> packages, string? project = null)
        {
            var rows = packages.ToDictionary(package => package.Key, package => new Dictionary<string, object>
            {
                ["type"] = direct.Contains(package.Key) ? "Direct" : "Transitive", ["resolved"] = "1.0.0",
                ["contentHash"] = Convert.ToBase64String(SHA512.HashData(File.ReadAllBytes(Path.Combine(PackageDirectory, package.Key.ToLowerInvariant() + ".1.0.0.nupkg")))),
                ["dependencies"] = package.Value
            });
            foreach (var id in direct) rows[id]["requested"] = "[1.0.0, 1.0.0]";
            File.WriteAllText(Path.Combine(project ?? ProjectDirectory, "packages.lock.json"), JsonSerializer.Serialize(new
                { version = 1, dependencies = new Dictionary<string, object> { [GameBuildProfile.TargetFramework] = rows } }));
        }
        public void Dispose()
        {
            if (Preserve) return;
            var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(Root).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Cleanup escaped temp root.");
            Directory.Delete(Root, true);
        }
    }
}
