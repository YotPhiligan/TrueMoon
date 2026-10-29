using System.Reflection;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using Xunit;

namespace TrueMoon.Aether.Tests;

public sealed class NativeRuntimeOutputTests
{
    [Theory]
    [InlineData("graceful")]
    [InlineData("exit")]
    [InlineData("kill")]
    public async Task Stop_ReapsChildDrainsOutputAndClosesAllAccessedStandardStreams(string mode)
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoon-stdio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var runtimePath = Path.Combine(AppContext.BaseDirectory, "EngineSessionProbe.dll");
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ??
            Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "../../..", "dotnet.exe"));
        var command = new NativeRuntimeLaunch(dotnet, runtimePath, [runtimePath, "--stdio-child", mode, directory]);
        var launch = new SessionLaunch(new(Guid.NewGuid(), 1, BuildIdentity.ForRuntime(runtimePath), BuildIdentity.PreviewId, new('A', 64)),
            "engine-stdio-" + Guid.NewGuid().ToString("N"), directory, Role: RuntimeRole.EditPreview);
        var factory = new NativeRuntimeFactory(command);
        await using var lease = Assert.IsType<NativeRuntimeLease>(factory.Create(launch));
        StreamReader? stdout = null, stderr = null;
        StreamWriter? stdin = null;
        try
        {
            await lease.LaunchAsync(CancellationToken.None);
            // Inspect actual OS stream handles; no fake process or handle-count/GC heuristic.
            var child = (EngineChild)typeof(NativeRuntimeLease).GetField("_child", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(lease)!;
            var process = child.OwnedProcess!;
            stdout = process.StandardOutput; stderr = process.StandardError; stdin = process.StandardInput;
            var outputHandle = ((FileStream)stdout.BaseStream).SafeFileHandle;
            var errorHandle = ((FileStream)stderr.BaseStream).SafeFileHandle;
            var inputHandle = ((FileStream)stdin.BaseStream).SafeFileHandle;
            using var ready = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!lease.StandardOutput.Contains("stdout-ready", StringComparison.Ordinal) ||
                   !lease.StandardError.Contains("stderr-ready", StringComparison.Ordinal)) await Task.Delay(10, ready.Token);
            if (mode == "exit")
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "exit"), "exit");
                await lease.Exited.WaitAsync(TimeSpan.FromSeconds(10));
            }

            await lease.StopAsync().WaitAsync(TimeSpan.FromSeconds(15));
            await lease.StopAsync();

            Assert.True(lease.Reaped);
            Assert.True(lease.ResourcesReleased);
            Assert.True(outputHandle.IsClosed, "The accessed stdout handle remained open after drain.");
            Assert.True(errorHandle.IsClosed, "The accessed stderr handle remained open after drain.");
            Assert.True(inputHandle.IsClosed, "The accessed stdin handle remained open after reap.");
            Assert.Equal("stdout-ready" + Environment.NewLine, lease.StandardOutput);
            Assert.Equal("stderr-ready" + Environment.NewLine, lease.StandardError);
            var diagnostic = Assert.Single(factory.ReadDiagnostics(1, 6));
            Assert.Equal(launch.Identity.SessionId, diagnostic.SessionId); Assert.Equal(launch.Identity.Generation, diagnostic.Generation);
            Assert.Equal("EditPreview", diagnostic.RuntimeRole); Assert.Equal(lease.ProcessId, diagnostic.ProcessId);
            Assert.True(diagnostic.Completed); Assert.True(diagnostic.ResourcesReleased); Assert.Null(diagnostic.Delivery);
            Assert.InRange(diagnostic.StandardOutput!.Output.Text.Length, 1, 6); Assert.True(diagnostic.StandardOutput.ReadFinished); Assert.True(diagnostic.StandardOutput.FileFinished);
            Assert.Equal(0, diagnostic.StandardOutput.Output.DroppedCharacters); Assert.True(diagnostic.StandardOutput.Output.OmittedCharacters > 0);
            Assert.True(diagnostic.StandardError!.ReadFinished); Assert.True(diagnostic.StandardError.FileFinished);
            Assert.DoesNotContain(launch.Identity.Nonce, System.Text.Json.JsonSerializer.Serialize(diagnostic));
            Assert.DoesNotContain(launch.Endpoint, System.Text.Json.JsonSerializer.Serialize(diagnostic));
            var output = Path.Combine(directory, "runtime-output");
            Assert.Equal(lease.StandardOutput, await File.ReadAllTextAsync(Path.Combine(output, "runtime-1.stdout.log")));
            Assert.Equal(lease.StandardError, await File.ReadAllTextAsync(Path.Combine(output, "runtime-1.stderr.log")));
        }
        finally
        {
            await lease.StopAsync();
            stdout?.Dispose(); stderr?.Dispose(); stdin?.Dispose();
        }
    }
}
