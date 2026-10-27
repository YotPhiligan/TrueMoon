using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace TrueMoon.Mithril.Tests;

public sealed class ReadyFileProcessTests
{
    private const string Payload = "{\"Pid\":1234,\"Parent\":5678,\"Unit\":7,\"Provider\":\"cobalt\",\"Endpoint\":\"ready-overlap-endpoint\"}";

    [Theory]
    [InlineData("composition")]
    [InlineData("restart")]
    public async Task ReadyRead_SharingViolationWaitsForCompleteJsonAndPreservesIdentity(string probe)
    {
        var directory = CreateDirectory();
        var bytes = Encoding.UTF8.GetBytes(Payload);
        var split = bytes.Length / 2;
        await using var writer = new FileStream(ReadyPath(probe, directory), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await writer.WriteAsync(bytes.AsMemory(0, split));
        await writer.FlushAsync();
        using var process = StartReader(probe, directory);
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await WaitForRetryAsync(process, errors);
            Assert.False(process.HasExited);
            await writer.WriteAsync(bytes.AsMemory(split));
            await writer.FlushAsync();
            await writer.DisposeAsync();
            await AssertIdentityResultAsync(process, errors, probe);
        }
        finally { await ReapAsync(process); }
    }

    [Theory]
    [InlineData("composition")]
    [InlineData("restart")]
    public async Task ReadyRead_MissingPublicationWaitsThenReadsPublishedIdentity(string probe)
    {
        var directory = CreateDirectory();
        var path = ReadyPath(probe, directory);
        using var process = StartReader(probe, directory);
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await WaitForRetryAsync(process, errors);
            Assert.False(process.HasExited);
            await File.WriteAllTextAsync(path + ".tmp", Payload);
            File.Move(path + ".tmp", path);
            await AssertIdentityResultAsync(process, errors, probe);
        }
        finally { await ReapAsync(process); }
    }

    [Theory]
    [InlineData("composition")]
    [InlineData("restart")]
    public async Task ReadyRead_SharingRetriesHonorCallerCancellation(string probe)
    {
        var directory = CreateDirectory();
        await using var writer = new FileStream(ReadyPath(probe, directory), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await writer.WriteAsync(Encoding.UTF8.GetBytes(Payload));
        await writer.FlushAsync();
        var elapsed = Stopwatch.StartNew();
        using var process = StartReader(probe, directory, cancelAfter: 600);
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await WaitForRetryAsync(process, errors);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, process.ExitCode);
            Assert.Contains("CanceledException", await errors.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Empty(await process.StandardOutput.ReadToEndAsync());
            Assert.InRange(elapsed.Elapsed.TotalSeconds, 0, 4);
        }
        finally { await ReapAsync(process); }
    }

    [Theory]
    [InlineData("composition")]
    [InlineData("restart")]
    public async Task ReadyRead_SharingRetriesDoNotExtendEightSecondDeadline(string probe)
    {
        var directory = CreateDirectory();
        await using var writer = new FileStream(ReadyPath(probe, directory), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await writer.WriteAsync(Encoding.UTF8.GetBytes(Payload));
        await writer.FlushAsync();
        using var process = StartReader(probe, directory);
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await WaitForRetryAsync(process, errors);
            var elapsed = Stopwatch.StartNew();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(12));
            Assert.Equal(1, process.ExitCode);
            Assert.Contains("CanceledException", await errors.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Empty(await process.StandardOutput.ReadToEndAsync());
            Assert.InRange(elapsed.Elapsed.TotalSeconds, 7, 12);
        }
        finally { await ReapAsync(process); }
    }

    [Theory]
    [InlineData("composition", "{", "JsonException")]
    [InlineData("restart", "{", "JsonException")]
    [InlineData("composition", "null", "InvalidDataException")]
    [InlineData("restart", "null", "InvalidDataException")]
    public async Task ReadyRead_InvalidPublishedJsonFailsWithoutRetry(string probe, string json, string errorType)
    {
        var directory = CreateDirectory();
        await File.WriteAllTextAsync(ReadyPath(probe, directory), json);
        using var process = StartReader(probe, directory);
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, process.ExitCode);
            Assert.Contains(errorType, await errors.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Empty(await output.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { await ReapAsync(process); }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "ready-file-results", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string ReadyPath(string probe, string directory)
        => Path.Combine(directory, probe == "composition" ? "ready.json" : "ready-1.json");

    private static Process StartReader(string probe, string directory, int cancelAfter = 0)
    {
        var fixture = probe == "composition" ? "MithrilCompositionProbe.dll" : "MithrilRestartProbe.dll";
        var info = new ProcessStartInfo("dotnet")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, probe + "-fixture", fixture));
        info.ArgumentList.Add("--read-ready");
        info.ArgumentList.Add(directory);
        info.ArgumentList.Add(cancelAfter.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Process.Start(info)!;
    }

    private static async Task WaitForRetryAsync(Process process, Task<string> errors)
    {
        var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        if (line != "RETRY") Assert.Fail($"Reader did not retry: {line}; {await errors.WaitAsync(TimeSpan.FromSeconds(5))}");
    }

    private static async Task AssertIdentityResultAsync(Process process, Task<string> errors, string probe)
    {
        var output = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var stderr = await errors.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(process.ExitCode == 0, $"Exit {process.ExitCode}: {stderr}");
        Assert.Empty(stderr);
        var line = Assert.Single((await output.WaitAsync(TimeSpan.FromSeconds(5))).Split('\n'), l => l.StartsWith("RESULT ", StringComparison.Ordinal));
        using var report = JsonDocument.Parse(line[7..]);
        var identity = report.RootElement;
        Assert.Equal(1234, identity.GetProperty("Pid").GetInt32());
        Assert.Equal(5678, identity.GetProperty("Parent").GetInt32());
        Assert.Equal(7, identity.GetProperty("Unit").GetInt32());
        Assert.Equal("ready-overlap-endpoint", identity.GetProperty("Endpoint").GetString());
        if (probe == "composition") Assert.Equal("cobalt", identity.GetProperty("Provider").GetString());
    }

    private static async Task ReapAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
