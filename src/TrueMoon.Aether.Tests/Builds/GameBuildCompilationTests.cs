using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using TrueMoon.Aether.Builds;
using Xunit;

namespace TrueMoon.Aether.Tests.Builds;

public sealed class GameBuildCompilationTests(GameBuildFixture builds) : IClassFixture<GameBuildFixture>
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CapturedCompiler_UsesOriginalBytesAfterSourcesAndReferencesAreReplaced(bool customPathMap)
    {
        using var f = new Files(); var compiler = f.CopyCompiler();
        var captured = await GameBuildCompilation.CaptureAsync(f.Inputs(), f.Preparation(customPathMap ? ["/pathmap:" + f.Root + "=/_/"] : []), f.Job, default);
        var originalHash = captured.Snapshot.Files.Single(file => file.Path == f.Source).Sha256;
        File.WriteAllText(f.Source, "invalid replacement source"); File.WriteAllText(f.Reference, "invalid replacement reference");
        File.WriteAllText(compiler, "invalid replacement compiler");
        Assert.NotEqual(originalHash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f.Source))));
        await Compile(captured.StartInfo);
        using var stream = File.OpenRead(Path.Combine(f.Job, "output", "Frozen.dll")); using var pe = new PEReader(stream); var metadata = pe.GetMetadataReader();
        var field = metadata.FieldDefinitions.Select(metadata.GetFieldDefinition).Single(field => metadata.GetString(field.Name) == "Value");
        var constant = metadata.GetConstant(field.GetDefaultValue());
        Assert.Equal(ConstantTypeCode.String, constant.TypeCode); Assert.Equal("before", Encoding.Unicode.GetString(metadata.GetBlobBytes(constant.Value)));
        var method = metadata.MethodDefinitions.Select(metadata.GetMethodDefinition).Single(item => metadata.GetString(item.Name) == "Path");
        var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!; var literal = Array.IndexOf(il, (byte)0x72);
        Assert.True(literal >= 0);
        var callerPath = metadata.GetUserString(MetadataTokens.UserStringHandle(BitConverter.ToInt32(il, literal + 1) & 0xffffff));
        var expectedPath = customPathMap ? "/_/Frozen.cs" : f.Source;
        Assert.True(expectedPath.Replace('\\', '/') == callerPath.Replace('\\', '/'), "Expected " + expectedPath + "; actual " + callerPath);
        Assert.Contains(captured.Snapshot.Files, file => file.Path.EndsWith("csc.dll", StringComparison.Ordinal));
        Assert.True(captured.Snapshot.TotalBytes > new FileInfo(f.Reference).Length);
        Assert.Throws<NotSupportedException>(() => ((IList<GameBuildInputFile>)captured.Snapshot.Files).Clear());
    }

    [Fact]
    public async Task CapturedSnapshot_PreservesRelativeConfigTreeAndMapsDiagnosticPaths()
    {
        using var f = new Files(); var config = Path.Combine(f.Root, ".editorconfig");
        File.WriteAllText(config, "root = true\n[*.cs]\ndotnet_diagnostic.CS0169.severity = error\n");
        var capture = await GameBuildCompilation.CaptureAsync(f.Inputs(), f.Preparation("/analyzerconfig:" + config), f.Job, default);
        var copies = capture.OriginalPaths.ToDictionary(p => p.Value, p => p.Key);
        Assert.Equal(Path.GetDirectoryName(copies[f.Source]), Path.GetDirectoryName(copies[config]));
        File.Delete(config); Assert.Contains("CS0169", File.ReadAllText(copies[config]));
        var args = File.ReadAllText(Path.Combine(f.Job, "capture", "compiler.rsp"));
        Assert.Contains("/pathmap:", args); Assert.Contains("/analyzerconfig:", args);
        Assert.Equal(f.Source, capture.OriginalPaths[copies[f.Source]]);
    }

    [Theory] [InlineData("@opaque.rsp")] [InlineData("/lib:ambient")] [InlineData("/resource:unknown.bin")]
    [InlineData("/analyzer:custom.dll")] [InlineData("/keyfile:key.snk")]
    public async Task OpaqueCompilerInputs_FailClosed(string option)
    {
        using var f = new Files();
        await Assert.ThrowsAsync<NotSupportedException>(() => GameBuildCompilation.CaptureAsync(f.Inputs(), f.Preparation(option), f.Job, default));
        Assert.False(File.Exists(Path.Combine(f.Job, "output", "Frozen.dll")));
    }

    [Theory] [InlineData("changed")] [InlineData("missing")] [InlineData("oversize")] [InlineData("cancelled")]
    public async Task InvalidOrCancelledCapture_DoesNotReturnPartialResult(string change)
    {
        using var f = new Files(); var inputs = f.Inputs();
        switch (change)
        {
            case "changed": File.AppendAllText(f.Source, "\n//changed"); await Assert.ThrowsAsync<IOException>(() => GameBuildCompilation.CaptureAsync(inputs, f.Preparation(), f.Job, default)); break;
            case "missing": File.Delete(f.Source); await Assert.ThrowsAsync<FileNotFoundException>(() => GameBuildCompilation.CaptureAsync(inputs, f.Preparation(), f.Job, default)); break;
            case "oversize":
                using (var stream = File.OpenWrite(f.Source)) stream.SetLength(GameBuildInputSnapshot.MaximumFileBytes + 1);
                await Assert.ThrowsAsync<InvalidDataException>(() => GameBuildCompilation.CaptureAsync(inputs, f.Preparation(), f.Job, default)); break;
            case "cancelled":
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GameBuildCompilation.CaptureAsync(inputs, f.Preparation(), f.Job, new(true)));
                Assert.False(Directory.Exists(f.Job)); break;
        }
    }

    [Fact]
    public async Task SdkCapturedBuild_PublishesExactPrivateDependencyAndReleasesStaging()
    {
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, Path.Combine(builds.Root, "capture-artifacts"), null, null, new() { MaxJobs = 1 });
        var request = new GameBuildRequest(builds.ProjectA, builds.A.EntryAssembly, builds.A.EntryType, 7, ["Game.Private.dll"]) { TrackInputs = true, CaptureCompilation = true };
        var first = await compiler.BuildAsync(request); var snapshot = Assert.IsType<GameBuildCompilationSnapshot>(first.Compilation);
        var buildDirectory = compiler.DiagnosticsDirectory!;
        Assert.True(File.Exists(Path.Combine(buildDirectory, "capture.json")));
        Assert.False(Directory.Exists(Path.Combine(buildDirectory, "capture"))); Assert.False(Directory.Exists(Path.Combine(buildDirectory, "work")));
        Assert.Null(compiler.ProcessId);
        await compiler.VerifyInputsAsync(first.Inputs!, snapshot);
        Assert.Contains(snapshot.Files, file => file.Path.EndsWith("Game.GlobalUsings.g.cs", StringComparison.Ordinal) || file.Path.EndsWith("Game.Candidate.GlobalUsings.g.cs", StringComparison.Ordinal));
        Assert.Contains(snapshot.Files, file => file.Path.EndsWith("System.Runtime.dll", StringComparison.Ordinal));
        Assert.Equal(File.ReadAllBytes(Path.Combine(builds.A.DirectoryPath, "Game.Private.dll")), File.ReadAllBytes(Path.Combine(first.DirectoryPath, "Game.Private.dll")));
        var second = await compiler.BuildAsync(request with { SourceRevision = 8 });
        Assert.Equal(snapshot.Fingerprint, second.Compilation!.Fingerprint); Assert.NotEqual(first.BuildId, second.BuildId);
        Assert.Equal(File.ReadAllBytes(Path.Combine(first.DirectoryPath, first.EntryAssembly)), File.ReadAllBytes(Path.Combine(second.DirectoryPath, second.EntryAssembly)));
        Assert.Null(GameBuildArtifact.Open(first.DirectoryPath, GameBuildFixture.Hash).Compilation);
        Assert.Equal(0, compiler.RetentionStatus!.SkippedDirectories); Assert.Equal(1, compiler.RetentionStatus.RetainedJobs);
    }

    [Fact]
    public async Task CapturedCompilerFailure_ReportsOriginalSourceLocationAndCleansWorkspace()
    {
        var project = builds.Project("capture-invalid", "Game.Invalid", "not valid C# source");
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, Path.Combine(builds.Root, "capture-invalid-artifacts"));
        await Assert.ThrowsAsync<GameBuildException>(() => compiler.BuildAsync(new(project, "Game.Invalid.dll", "Game.Candidate.Game", 1) { TrackInputs = true, CaptureCompilation = true }));
        var report = compiler.ReadDiagnostics(1, 0).Single().CompilerDiagnostics!;
        Assert.Equal(GameBuildDiagnosticState.Available, report.State);
        Assert.Contains(report.Entries, item => item.Severity == GameBuildDiagnosticSeverity.Error && item.FilePath == Path.Combine(Path.GetDirectoryName(project)!, "Game.cs") && item.Line == 1);
        Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "capture"))); Assert.Null(compiler.ProcessId);
    }

    [Fact]
    public async Task CancelDuringPreparation_ReapsSdkAndRetainsNoCapturedWorkspace()
    {
        var held = builds.HeldProject(); await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, Path.Combine(builds.Root, "capture-cancel-artifacts"));
        using var token = new CancellationTokenSource();
        var building = compiler.BuildAsync(new(held.Project, "Game.Held.dll", "Game.Candidate.Game", 1) { TrackInputs = true, CaptureCompilation = true }, token.Token);
        try
        {
            await TestWait.UntilAsync(() => File.Exists(held.Marker), TimeSpan.FromSeconds(25)); var pid = compiler.ProcessId!.Value;
            token.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => building);
            Assert.Null(compiler.ProcessId); Assert.True(Exited(pid)); Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "capture")));
        }
        finally { File.WriteAllText(held.Release, "release"); }
    }

    [Fact]
    public async Task CaptureWithoutTracking_IsRejectedBeforeAnySdkJob()
    {
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, Path.Combine(builds.Root, "capture-invalid-options"));
        Assert.Throws<ArgumentException>(() => { _ = compiler.BuildAsync(new(builds.ProjectA, builds.A.EntryAssembly, builds.A.EntryType, 1) { CaptureCompilation = true }); });
        Assert.Null(compiler.ProcessId); Assert.Null(compiler.DiagnosticsDirectory);
    }

    [Theory] [InlineData("output")] [InlineData("target")] [InlineData("missing")] [InlineData("environment")]
    public async Task InvalidCompilerInvocation_IsRejectedBeforeCompilation(string kind)
    {
        using var f = new Files(); var preparation = f.Preparation();
        preparation = kind switch
        {
            "output" => preparation.Replace("/out:Frozen.dll", "/out:Other.dll"),
            "target" => preparation.Replace("/target:library", "/target:exe"),
            "missing" => preparation.Replace("/out:Frozen.dll", "/optimize+"),
            _ => preparation.Replace("\"CscEnvironment\":\"\"", "\"CscEnvironment\":\"custom\"")
        };
        if (kind == "target" || kind == "environment") await Assert.ThrowsAsync<NotSupportedException>(() => GameBuildCompilation.CaptureAsync(f.Inputs(), preparation, f.Job, default));
        else await Assert.ThrowsAsync<InvalidDataException>(() => GameBuildCompilation.CaptureAsync(f.Inputs(), preparation, f.Job, default));
        Assert.False(File.Exists(Path.Combine(f.Job, "output", "Frozen.dll")));
    }

    [Fact]
    public async Task AbsoluteAnalyzerConfigPatterns_AreRejectedInsteadOfChangingTheirMeaning()
    {
        using var f = new Files(); var config = Path.Combine(f.Root, ".globalconfig"); File.WriteAllText(config, "is_global = true\n[C:/absolute/Frozen.cs]\n");
        await Assert.ThrowsAsync<NotSupportedException>(() => GameBuildCompilation.CaptureAsync(f.Inputs(), f.Preparation("/analyzerconfig:" + config), f.Job, default));
    }

    [Theory] [InlineData("reference")] [InlineData("compiler")] [InlineData("generated")]
    public async Task Verification_RechecksStableCompilerInputsButDoesNotRequireDeletedJobFiles(string change)
    {
        using var f = new Files(); var compiler = f.CopyCompiler(); var generated = Path.Combine(f.Job, "work", "generated.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(generated)!); File.WriteAllText(generated, "generated data");
        var capture = await GameBuildCompilation.CaptureAsync(f.Inputs(), f.Preparation("/additionalfile:" + generated), f.Job, default);
        if (change == "generated")
        {
            File.Delete(generated); await capture.Snapshot.VerifyOriginalsAsync(default);
            Assert.Contains(capture.Snapshot.Files, file => file.Path == generated);
        }
        else
        {
            File.WriteAllText(change == "compiler" ? compiler : f.Reference, "changed");
            var error = await Assert.ThrowsAsync<GameBuildInputsChangedException>(() => capture.Snapshot.VerifyOriginalsAsync(default));
            Assert.Equal(capture.Snapshot.Fingerprint, error.Expected); Assert.NotEqual(error.Expected, error.Actual);
        }
    }

    [Fact]
    public async Task Verification_RejectsCaptureFromAnotherWitnessBeforeStartingSdk()
    {
        using var f = new Files(); using var other = new Files();
        var capture = await GameBuildCompilation.CaptureAsync(f.Inputs(), f.Preparation(), f.Job, default);
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, Path.Combine(f.Root, "artifacts"));
        Assert.Throws<ArgumentException>(() => { _ = compiler.VerifyInputsAsync(other.Inputs(), capture.Snapshot); });
        Assert.Null(compiler.ProcessId); Assert.Null(compiler.DiagnosticsDirectory); Assert.Empty(compiler.ReadDiagnostics(16, 0));
    }

    [Fact]
    public async Task UnsupportedSdkCompilerSearchPath_FailsClosedAndCleansPreparation()
    {
        var project = builds.Project("capture-unsupported", "Game.Unsupported", GameBuildFixture.Source("0xff2080c0u"),
            target: "<PropertyGroup><AdditionalLibPaths>$(MSBuildProjectDirectory)</AdditionalLibPaths></PropertyGroup>");
        await using var compiler = new GameBuildCompiler(GameBuildFixture.Hash, Path.Combine(builds.Root, "capture-unsupported-artifacts"));
        var error = await Assert.ThrowsAsync<NotSupportedException>(() => compiler.BuildAsync(new(project, "Game.Unsupported.dll", "Game.Candidate.Game", 1) { TrackInputs = true, CaptureCompilation = true }));
        Assert.Contains("lib", error.Message); Assert.Null(compiler.ProcessId);
        Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "capture"))); Assert.False(Directory.Exists(Path.Combine(compiler.DiagnosticsDirectory!, "work")));
        Assert.True(compiler.ReadDiagnostics(1, 0).Single().ResourcesReleased);
    }

    private static async Task Compile(ProcessStartInfo info)
    {
        using var process = Process.Start(info)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(process.ExitCode == 0, await stdout + "\n" + await stderr);
        }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } }
    }
    private static bool Exited(int pid) { try { using var p = Process.GetProcessById(pid); return p.HasExited; } catch (ArgumentException) { return true; } }

    private sealed class Files : IDisposable
    {
        private string? _sdk;
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "TrueMoon-CscCapture-" + Guid.NewGuid().ToString("N"));
        public string Source => Path.Combine(Root, "Frozen.cs"); public string Reference => Path.Combine(Root, "Reference.dll");
        public string Project => Path.Combine(Root, "Frozen.csproj"); public string Job => Path.Combine(Root, "job");
        public Files()
        {
            Directory.CreateDirectory(Root); File.WriteAllText(Project, "<Project/>");
            File.WriteAllText(Source, "public static class Frozen { public const string Value = \"before\"; public static string Path() => At(); private static string At([System.Runtime.CompilerServices.CallerFilePath] string path = \"\") => path; }"); File.Copy(typeof(object).Assembly.Location, Reference);
        }
        public GameBuildInputSnapshot Inputs() => GameBuildInputSnapshot.Create(new(Project, "Frozen.dll", "Frozen", 1), JsonSerializer.Serialize(new
        {
            Properties = new { TargetFramework = "net10.0" }, Items = new { Compile = new[] { new { FullPath = Source } }, Reference = Array.Empty<object>(), ProjectReference = Array.Empty<object>(), PackageReference = Array.Empty<object>() }
        }), "project", Job);
        public string Preparation(params string[] extra)
        {
            var dotnetRoot = new DirectoryInfo(Path.GetDirectoryName(typeof(object).Assembly.Location)!).Parent!.Parent!.Parent!.FullName;
            var sdk = _sdk ?? InstalledSdk();
            return JsonSerializer.Serialize(new
            {
                Properties = new { CscToolPath = "", CscToolExe = "", CscEnvironment = "", MSBuildToolsPath = sdk, RoslynTargetsPath = Path.Combine(sdk, "Roslyn"), NetCoreTargetingPackRoot = Path.Combine(dotnetRoot, "packs"), NETCoreSdkVersion = Path.GetFileName(sdk) },
                Items = new { CscCommandLineArgs = new[] { "/noconfig", "/nostdlib+", "/reference:" + Reference, "/target:library", "/out:Frozen.dll", Source }.Concat(extra).Select(value => new { Identity = value }).ToArray(), ReferenceCopyLocalPaths = Array.Empty<object>() }
            });
        }
        public string CopyCompiler()
        {
            _sdk = Path.Combine(Root, "sdk"); var compiler = Path.Combine(_sdk, "Roslyn", "bincore"); Directory.CreateDirectory(compiler);
            foreach (var name in new[] { "csc.dll", "csc.deps.json", "csc.runtimeconfig.json", "Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis.CSharp.dll" })
                File.Copy(Path.Combine(InstalledSdk(), "Roslyn", "bincore", name), Path.Combine(compiler, name));
            return Path.Combine(compiler, "csc.dll");
        }
        private static string InstalledSdk()
        {
            var dotnetRoot = new DirectoryInfo(Path.GetDirectoryName(typeof(object).Assembly.Location)!).Parent!.Parent!.Parent!.FullName;
            return Directory.EnumerateDirectories(Path.Combine(dotnetRoot, "sdk")).Where(path => File.Exists(Path.Combine(path, "Roslyn", "bincore", "csc.dll")))
                .OrderBy(path => Version.Parse(Path.GetFileName(path).Split('-')[0])).Last();
        }
        public void Dispose()
        {
            var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(Root).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected cleanup root.");
            Directory.Delete(Root, true);
        }
    }
}
