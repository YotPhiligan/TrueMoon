using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace TrueMoon.Tests;

public sealed class ProcessOutputFileWorkerTests
{
    [Fact]
    public async Task FileDrain_PoolWorkerHeldAfterEof_CompletesAndPreservesBorrowedReader()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoon-file-worker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Path.GetFullPath(Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(), "../../..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
        var probe = Path.Combine(AppContext.BaseDirectory, "process-output-probe", "ProcessOutputProbe.dll");
        Assert.True(File.Exists(probe), "The Core-only process fixture was not copied by the test build.");
        var info = new ProcessStartInfo(dotnet)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { probe, "--starvation", directory }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new IOException("ProcessOutputProbe did not start.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));
            Assert.True(process.ExitCode == 0, $"File-worker proof failed: {await error}\nEvidence: {directory}");
            Assert.Empty(await error);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "report.json")));
            var result = document.RootElement;
            Assert.True(result.GetProperty("Passed").GetBoolean());
            Assert.True(result.GetProperty("ReadCompleted").GetBoolean());
            Assert.True(result.GetProperty("FileCompletedWhileWorkerHeld").GetBoolean());
            Assert.True(result.GetProperty("CompletedWhileWorkerHeld").GetBoolean());
            Assert.False(result.GetProperty("ReaderDisposed").GetBoolean());
            Assert.Equal(1, result.GetProperty("WorkerCount").GetInt32());
            var state = result.GetProperty("State"); var snapshot = state.GetProperty("Snapshot");
            Assert.Equal("Completed", state.GetProperty("FileStage").GetString());
            Assert.Equal("PACKAGE_RUNTIME_STDOUT\r\n", snapshot.GetProperty("Text").GetString());
            Assert.Equal(24, snapshot.GetProperty("TotalCharacters").GetInt64());
            Assert.Equal(24, snapshot.GetProperty("FileBytesWritten").GetInt64());
            Assert.Equal(0, snapshot.GetProperty("FileBytesDropped").GetInt64());
            Assert.Equal(JsonValueKind.Null, snapshot.GetProperty("FileError").ValueKind);
            Assert.Equal("PACKAGE_RUNTIME_STDOUT\r\n", await File.ReadAllTextAsync(Path.Combine(directory, "capture.log")));
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            await File.WriteAllTextAsync(Path.Combine(directory, "stdout.log"), await output.WaitAsync(TimeSpan.FromSeconds(5)));
            await File.WriteAllTextAsync(Path.Combine(directory, "stderr.log"), await error.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }
}
