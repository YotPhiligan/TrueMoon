using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Mithril;

namespace TrueMoon.ManualTests.LaunchProfiles;

internal static class IdentityFaults
{
    internal static async Task<object> RejectAsync(NativeRuntimeLaunch runtime, string hostId, string directory, bool wrongEntry, CancellationToken token)
    {
        var name = wrongEntry ? "claimed-entry-mismatch" : "host-fingerprint-mismatch";
        var report = Path.Combine(directory, name); Directory.CreateDirectory(report);
        var host = Assembly.GetEntryAssembly()!.Location;
        var claimedRuntime = wrongEntry ? typeof(RuntimeBootstrap).Assembly.Location : runtime.RuntimeAssemblyPath;
        var fingerprint = wrongEntry ? BuildIdentity.ForRuntime(claimedRuntime) : (hostId[0] == '0' ? "1" : "0") + hostId[1..];
        var launch = new SessionLaunch(new(Guid.NewGuid(), 1, fingerprint, BuildIdentity.PreviewId, Convert.ToHexString(RandomNumberGenerator.GetBytes(32))),
            "engine-launch-negative-" + Guid.NewGuid().ToString("N"), report, Role: RuntimeRole.EditPreview)
        { RuntimeIdentity = new(host, claimedRuntime) };
        var info = new ProcessStartInfo(runtime.ExecutablePath) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = runtime.WorkingDirectory };
        foreach (var argument in runtime.Arguments) info.ArgumentList.Add(argument);
        info.Environment[NativeRuntimeFactory.BootstrapKey] = RuntimeBootstrap.Encode(launch);
        info.Environment["-p"] = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        info.Environment.Remove(UnitParentProcessEventsHandler.ControlPipeKey);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Negative child did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token); limit.CancelAfter(TimeSpan.FromSeconds(15));
        try { await process.WaitForExitAsync(limit.Token); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        var error = await stderr;
        await File.WriteAllTextAsync(Path.Combine(report, "stdout.log"), await stdout, token);
        await File.WriteAllTextAsync(Path.Combine(report, "stderr.log"), error, token);
        if (process.ExitCode != 1 || !error.Contains("fingerprint", StringComparison.OrdinalIgnoreCase) ||
            Directory.Exists(Path.Combine(report, "diagnostics")) || Directory.GetFiles(report, "runtime-*.json").Length != 0)
            throw new InvalidOperationException("Actual child did not reject identity before App: " + error);
        return new { Name = name, Passed = true, process.Id, process.ExitCode, Reaped = process.HasExited, RejectedBeforeApp = true };
    }
}
