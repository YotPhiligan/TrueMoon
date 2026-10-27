using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Xunit;

namespace TrueMoon.Aether.Tests;

/// <summary>Actual process acceptance for distinct managed parent/runtime entries and explicit native commands.</summary>
[SupportedOSPlatform("windows")]
public sealed class NativeRuntimeProcessTests
{
    /// <summary>Exercises the same headless independent-runtime proof through dotnet DLL and Windows apphost commands.</summary>
    /// <param name="extension">Selects a managed DLL command or its executable apphost.</param>
    /// <returns>Completes after every owned proof process has exited and its reports have been checked.</returns>
    [Theory]
    [InlineData(".dll")]
    [InlineData(".exe")]
    public async Task IndependentRuntime_PreservesExactIdentityRoleIsolationAndOwnedCleanup(string extension)
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoon native runtime proof " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var deployment = Path.Combine(directory, "standalone runtime");
        StageRuntime(deployment);
        var reportDirectory = Path.Combine(directory, "proof reports");
        var runtime = Path.Combine(deployment, "AetherRuntimeHost" + extension);
        Assert.True(File.Exists(runtime), $"Missing runtime command: {runtime}");
        var info = new ProcessStartInfo(DotnetExecutable())
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "EngineSessionProbe.dll"));
        info.ArgumentList.Add("--runtime-launch-proof"); info.ArgumentList.Add(runtime); info.ArgumentList.Add(reportDirectory);
        using var process = new Process { StartInfo = info };
        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        var passed = false;
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(130));
            Assert.True(process.ExitCode == 0, $"Runtime proof exit {process.ExitCode}: {await stderr}\n{await stdout}\nArtifacts: {directory}");
            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(reportDirectory, "runtime-launch-report.json")));
            var root = report.RootElement;
            Assert.True(root.GetProperty("Passed").GetBoolean());
            Assert.Equal("independent-runtime-launch", root.GetProperty("Scenario").GetString());
            Assert.Equal(extension == ".exe" ? "apphost" : "dotnet", root.GetProperty("RuntimeCommandKind").GetString());
            Assert.Equal(process.Id, root.GetProperty("HostPid").GetInt32());
            Assert.NotEqual(Environment.ProcessId, process.Id);
            var hostEntry = root.GetProperty("HostAssemblyPath").GetString()!;
            var runtimeEntry = root.GetProperty("RuntimeAssemblyPath").GetString()!;
            AssertPath(Path.Combine(deployment, "AetherRuntimeHost.dll"), runtimeEntry);
            Assert.False(string.Equals(hostEntry, runtimeEntry, StringComparison.OrdinalIgnoreCase));
            Assert.False(string.Equals(Path.GetDirectoryName(hostEntry), Path.GetDirectoryName(runtimeEntry), StringComparison.OrdinalIgnoreCase));
            Assert.False(root.GetProperty("RuntimeDirectoryHasGameFile").GetBoolean());
            Assert.False(root.GetProperty("GameLoadedInParent").GetBoolean());
            Assert.NotEqual(root.GetProperty("SameExecutableHostId").GetString(), root.GetProperty("HostContractId").GetString());
            var artifact = root.GetProperty("Artifact");
            Assert.False(artifact.GetProperty("CompilerStarted").GetBoolean());
            var artifactDirectory = artifact.GetProperty("DirectoryPath").GetString()!;
            var generations = root.GetProperty("Generations").EnumerateArray().ToArray();
            Assert.Equal(3, generations.Length);
            var pids = new HashSet<int>();
            for (var index = 0; index < generations.Length; index++)
            {
                var generation = generations[index]; var game = index == 2;
                Assert.Equal((ulong)index + 1, generation.GetProperty("Generation").GetUInt64());
                Assert.Equal(generations[0].GetProperty("SessionId").GetGuid(), generation.GetProperty("SessionId").GetGuid());
                Assert.Equal(game ? "Game" : "EditPreview", generation.GetProperty("Role").GetString());
                Assert.True(generation.GetProperty("HelloAccepted").GetBoolean()); Assert.True(generation.GetProperty("SceneLoaded").GetBoolean());
                Assert.Equal(game ? 6 : 5, generation.GetProperty("Controls").GetInt32());
                Assert.Equal(game ? 1ul : 0ul, generation.GetProperty("Tick").GetUInt64());
                var pid = generation.GetProperty("ProcessId").GetInt32(); Assert.True(pids.Add(pid)); Assert.NotEqual(process.Id, pid);
                AssertReleased(generation);
                var metadata = generation.GetProperty("Metadata");
                Assert.Equal(pid, metadata.GetProperty("Pid").GetInt32());
                Assert.Equal(process.Id, metadata.GetProperty("ParentProcessId").GetInt32());
                AssertPath(hostEntry, metadata.GetProperty("HostAssemblyPath").GetString()!);
                AssertPath(runtimeEntry, metadata.GetProperty("EntryAssemblyPath").GetString()!);
                AssertPath(runtimeEntry, metadata.GetProperty("RuntimeAssemblyPath").GetString()!);
                Assert.Equal(root.GetProperty("HostContractId").GetString(), metadata.GetProperty("HostContractId").GetString());
                Assert.False(metadata.GetProperty("EditorLoaded").GetBoolean()); Assert.False(metadata.GetProperty("UiLoaded").GetBoolean());
                Assert.Equal(game, metadata.GetProperty("GameLoaded").GetBoolean());
                Assert.Equal(0, metadata.GetProperty("Resources").GetInt32()); Assert.Equal(0, metadata.GetProperty("Handles").GetInt32());
                Assert.Equal(JsonValueKind.Null, metadata.GetProperty("Error").ValueKind);
                Assert.True(File.Exists(generation.GetProperty("MetadataFile").GetString()));
                var paths = metadata.GetProperty("GameAssemblyPaths").EnumerateArray().ToArray();
                if (game)
                {
                    AssertPath(Path.Combine(artifactDirectory, "Game.Logic.dll"), Assert.Single(paths).GetString()!);
                    Assert.Equal(artifact.GetProperty("BuildId").GetString(), metadata.GetProperty("GameBuildId").GetString());
                }
                else
                {
                    Assert.Empty(paths); Assert.Equal(JsonValueKind.Null, metadata.GetProperty("Build").ValueKind);
                    Assert.False(metadata.GetProperty("RequireRestore").GetBoolean());
                }
            }
            var cases = root.GetProperty("Cases").EnumerateArray().ToDictionary(value => value.GetProperty("Name").GetString()!);
            Assert.Equal(5, cases.Count);
            foreach (var name in new[] { "host-fingerprint", "child-host-fingerprint", "child-actual-entry", "startup-cancel", "immediate-exit" })
            {
                Assert.True(cases[name].GetProperty("Passed").GetBoolean()); AssertReleased(cases[name]);
            }
            Assert.Equal(0, cases["host-fingerprint"].GetProperty("ProcessId").GetInt32());
            Assert.True(cases["host-fingerprint"].GetProperty("RejectedBeforeProcess").GetBoolean());
            foreach (var name in new[] { "child-host-fingerprint", "child-actual-entry" })
            {
                var rejection = cases[name];
                Assert.True(rejection.GetProperty("ProcessId").GetInt32() > 0);
                Assert.True(rejection.GetProperty("ParentPrecheckBypassed").GetBoolean());
                Assert.True(rejection.GetProperty("RejectedBeforeApp").GetBoolean());
                Assert.Equal(1, rejection.GetProperty("ExitCode").GetInt32());
                Assert.Contains("InvalidDataException", await File.ReadAllTextAsync(rejection.GetProperty("StderrFile").GetString()!));
            }
            foreach (var name in new[] { "startup-cancel", "immediate-exit" })
            {
                Assert.Equal("Hello", cases[name].GetProperty("Phase").GetString());
                Assert.False(cases[name].GetProperty("HelloAccepted").GetBoolean()); Assert.False(cases[name].GetProperty("SceneLoaded").GetBoolean());
                Assert.True(cases[name].GetProperty("ProcessId").GetInt32() > 0);
            }
            Assert.True(cases["startup-cancel"].GetProperty("CancellationAfterLaunch").GetBoolean());
            Assert.Contains("Usage: AetherRuntimeHost", cases["immediate-exit"].GetProperty("StandardError").GetString());
            var same = root.GetProperty("SameExecutable");
            Assert.True(same.GetProperty("Passed").GetBoolean()); Assert.True(same.GetProperty("IdentityMatchesLegacyHostId").GetBoolean());
            Assert.Equal(root.GetProperty("SameExecutableHostId").GetString(), same.GetProperty("HostContractId").GetString());
            AssertPath(hostEntry, same.GetProperty("RuntimeAssemblyPath").GetString()!);
            AssertReleased(same.GetProperty("Generation"));
            Assert.False(same.GetProperty("Generation").GetProperty("Metadata").GetProperty("GameLoaded").GetBoolean());
            passed = true;
        }
        finally
        {
            try
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            }
            finally
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "parent.stdout.log"), await stdout.WaitAsync(TimeSpan.FromSeconds(10)));
                await File.WriteAllTextAsync(Path.Combine(directory, "parent.stderr.log"), await stderr.WaitAsync(TimeSpan.FromSeconds(10)));
                if (passed) Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void AssertReleased(JsonElement value)
    {
        Assert.True(value.GetProperty("Reaped").GetBoolean()); Assert.True(value.GetProperty("ResourcesReleased").GetBoolean());
    }

    private static void AssertPath(string expected, string actual)
        => Assert.Equal(Path.GetFullPath(expected), Path.GetFullPath(actual), StringComparer.OrdinalIgnoreCase);

    private static string DotnetExecutable()
    {
        var current = Environment.ProcessPath;
        if (current != null && Path.GetFileNameWithoutExtension(current).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) return current;
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured) && File.Exists(configured)) return configured;
        var installed = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
        Assert.True(File.Exists(installed), "Cannot locate an absolute dotnet executable; set DOTNET_HOST_PATH for the process proof.");
        return installed;
    }

    private static void StageRuntime(string directory)
    {
        Directory.CreateDirectory(directory);
        var source = AppContext.BaseDirectory;
        var depsFile = Path.Combine(source, "AetherRuntimeHost.deps.json");
        Assert.True(File.Exists(depsFile), "Build the runtime ProjectReference before running this process test.");
        using var deps = JsonDocument.Parse(File.ReadAllText(depsFile));
        var root = deps.RootElement;
        Assert.DoesNotContain(root.GetProperty("libraries").EnumerateObject(), library =>
            library.Name.StartsWith("TrueMoon.Aether.Editor/", StringComparison.OrdinalIgnoreCase) ||
            library.Name.StartsWith("TrueMoon.Alloy", StringComparison.OrdinalIgnoreCase) ||
            library.Name.StartsWith("TrueMoon.Argentis", StringComparison.OrdinalIgnoreCase) ||
            library.Name.StartsWith("Game.Logic/", StringComparison.OrdinalIgnoreCase));
        var target = root.GetProperty("targets").GetProperty(root.GetProperty("runtimeTarget").GetProperty("name").GetString()!);
        // Stage only the standalone managed dependency closure, not the merged test/probe
        // output (which includes UI and Game.Logic). No native/GPU assets are exercised.
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "AetherRuntimeHost.dll", "AetherRuntimeHost.exe", "AetherRuntimeHost.deps.json", "AetherRuntimeHost.runtimeconfig.json" };
        foreach (var library in target.EnumerateObject())
            if (library.Value.TryGetProperty("runtime", out var assets))
                foreach (var asset in assets.EnumerateObject())
                    if (asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) files.Add(Path.GetFileName(asset.Name));
        foreach (var file in files)
        {
            var path = Path.Combine(source, file);
            Assert.True(File.Exists(path), $"Missing standalone runtime dependency: {path}");
            File.Copy(path, Path.Combine(directory, file));
        }
        Assert.False(File.Exists(Path.Combine(directory, "Game.Logic.dll")));
    }
}
