using System.Diagnostics;
using System.Text.Json;
using TrueMoon.Diagnostics;
using Xunit;

namespace TrueMoon.Aether.Tests.Diagnostics;

public class ChildDiagnosticProcessTests
{
    [Fact]
    public async Task ThreeActualGenerations_ForwardProvenanceAndLossWhileControlAndLocalFilesSurviveFloodAndBrokenSinks()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoon-child-delivery-process-" + Guid.NewGuid().ToString("N"));
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "EngineSessionProbe.dll")); info.ArgumentList.Add("--child-diagnostics-proof"); info.ArgumentList.Add(directory);
        using var process = new Process { StartInfo = info };
        Assert.True(process.Start()); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); var passed = false;
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90));
            Assert.True(process.ExitCode == 0, $"Delivery proof exit {process.ExitCode}: {await stderr}\n{await stdout}\nArtifacts: {directory}");
            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "delivery-report.json")));
            var root = report.RootElement; Assert.True(root.GetProperty("Passed").GetBoolean()); Assert.Equal("child-diagnostic-delivery", root.GetProperty("Scenario").GetString());
            Assert.NotEqual(Environment.ProcessId, root.GetProperty("HostPid").GetInt32());
            var hostApp = root.GetProperty("HostApplicationId").GetGuid(); var session = root.GetProperty("SessionId").GetGuid();
            var generations = root.GetProperty("Generations").EnumerateArray().ToArray(); Assert.Equal(3, generations.Length);
            var applications = new HashSet<Guid>(); var pids = new HashSet<int>();
            foreach (var generation in generations)
            {
                var number = generation.GetProperty("Generation").GetUInt64(); var pid = generation.GetProperty("ProcessId").GetInt32();
                Assert.True(pids.Add(pid)); Assert.NotEqual(root.GetProperty("HostPid").GetInt32(), pid);
                Assert.True(generation.GetProperty("Reaped").GetBoolean()); Assert.True(generation.GetProperty("ResourcesReleased").GetBoolean()); Assert.Equal(33, generation.GetProperty("Controls").GetInt32());
                var receiver = generation.GetProperty("Receiver"); Assert.True(receiver.GetProperty("Released").GetBoolean()); Assert.True(receiver.GetProperty("Closed").GetBoolean());
                Assert.True(receiver.GetProperty("Accepted").GetInt64() > 0); Assert.True(receiver.GetProperty("ChildReportedLost").GetInt64() > 0);
                using var child = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, $"delivery-child-{number}.json")));
                var metadata = child.RootElement; Assert.Equal(pid, metadata.GetProperty("ProcessId").GetInt32()); Assert.False(metadata.GetProperty("GameLoaded").GetBoolean());
                var childApp = metadata.GetProperty("ApplicationId").GetGuid(); Assert.True(applications.Add(childApp)); Assert.NotEqual(hostApp, childApp);
                Assert.True(metadata.GetProperty("MemoryEvicted").GetInt64() > 0); Assert.True(metadata.GetProperty("Emitted").GetInt32() > receiver.GetProperty("Accepted").GetInt64());
                var sender = metadata.GetProperty("Sender"); Assert.True(sender.GetProperty("Closed").GetBoolean()); Assert.True(sender.GetProperty("Released").GetBoolean()); Assert.True(sender.GetProperty("Lost").GetInt64() > 0);
                var localFiles = metadata.GetProperty("LocalFiles").EnumerateArray().ToArray(); Assert.InRange(localFiles.Length, 1, 2);
                Assert.All(localFiles, file => Assert.InRange(file.GetProperty("Bytes").GetInt64(), 1, 32768));
                var rows = generation.GetProperty("Imported").Deserialize<DiagnosticRecord[]>()!; Assert.NotEmpty(rows);
                Assert.All(rows, row =>
                {
                    Assert.Equal(hostApp, row.ApplicationId); Assert.Equal(childApp, row.Origin!.ApplicationId); Assert.True(row.Origin.Sequence > 0); Assert.Equal(pid, row.ProcessId);
                    Assert.Equal(metadata.GetProperty("TraceId").GetString(), row.TraceId); Assert.Equal(metadata.GetProperty("SpanId").GetString(), row.SpanId);
                    Assert.InRange(row.TimestampUtc, metadata.GetProperty("Started").GetDateTimeOffset(), metadata.GetProperty("Finished").GetDateTimeOffset());
                    Assert.Equal(session.ToString("D"), row.Properties["SessionId"]); Assert.Equal(number.ToString(), row.Properties["Generation"]); Assert.Equal("EditPreview", row.Properties["RuntimeRole"]);
                    Assert.DoesNotContain(row.Properties.Keys, key => key.Equals("Nonce", StringComparison.OrdinalIgnoreCase) || key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase));
                });
                Assert.Equal(rows.Length, rows.Select(x => x.Origin!.Sequence).Distinct().Count());
                foreach (var stream in new[] { "Stdout", "Stderr" })
                {
                    var capture = generation.GetProperty(stream); Assert.True(capture.GetProperty("TotalCharacters").GetInt64() >= 96 * 1024);
                    Assert.InRange(capture.GetProperty("Text").GetString()!.Length, 1, 32768); Assert.True(capture.GetProperty("IsTruncated").GetBoolean());
                    Assert.InRange(capture.GetProperty("FileBytesWritten").GetInt64(), 1, 1024 * 1024);
                }
            }
            var health = root.GetProperty("Health").EnumerateArray().ToArray();
            Assert.Contains(health, x => x.GetProperty("Name").GetString() == "custom-0" && x.GetProperty("Dropped").GetInt64() > 0);
            Assert.Contains(health, x => x.GetProperty("Name").GetString() == "custom-1" && x.GetProperty("Failures").GetInt64() > 0);
            var rawFiles = root.GetProperty("RawFiles").EnumerateArray().ToArray(); Assert.Equal(6, rawFiles.Length);
            Assert.All(rawFiles, file => Assert.InRange(file.GetProperty("Bytes").GetInt64(), 1, 1024 * 1024));
            passed = true;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            // Preserve failure artifacts; successful runs use only this generated OS-temp directory.
            if (passed) Directory.Delete(directory, recursive: true);
        }
    }
}
