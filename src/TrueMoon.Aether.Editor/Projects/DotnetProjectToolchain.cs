using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace TrueMoon.Aether.Editor.Projects;

/// <summary>Runs a bounded, owned dotnet --version probe in the game directory. No game code or MSBuild targets execute.</summary>
public sealed class DotnetProjectToolchain : IProjectToolchain
{
    /// <inheritdoc />
    public async Task<ProjectToolchainInfo> ValidateAsync(string gameProjectPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameProjectPath);
        var path = Path.GetFullPath(gameProjectPath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path)) throw new FileNotFoundException("Game project is missing.", path);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var end = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetDirectoryName(path)!,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("--version");
        using var process = new Process { StartInfo = info };
        Task<string> output = Task.FromResult(""), error = Task.FromResult("");
        var started = false;
        try
        {
            try { started = process.Start(); }
            catch (Win32Exception cause) { throw new IOException("Cannot start dotnet. Install the .NET 10 SDK and make dotnet available on PATH.", cause); }
            if (!started) throw new IOException("The dotnet SDK probe did not start.");
            output = ReadBoundedAsync(process.StandardOutput);
            error = ReadBoundedAsync(process.StandardError);
            await process.WaitForExitAsync(end.Token).ConfigureAwait(false);
            end.Token.ThrowIfCancellationRequested();
            var sdk = (await output.ConfigureAwait(false)).Trim();
            var diagnostics = (await error.ConfigureAwait(false)).Trim();
            if (process.ExitCode != 0 || sdk.Length > 128 ||
                !Version.TryParse(sdk.Split('-')[0], out var version) || version.Major < 10)
                throw new IOException($"A .NET 10 or newer SDK must be selected in '{info.WorkingDirectory}'. Check PATH/global.json. " +
                    $"dotnet --version exit {process.ExitCode}: {sdk} {diagnostics}");
            return new(sdk);
        }
        catch (OperationCanceledException cause) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        { throw new TimeoutException($"dotnet SDK preflight exceeded 15 seconds for '{path}'.", cause); }
        finally
        {
            if (started)
            {
                if (!process.HasExited)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) when (process.HasExited) { }
                }
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        var result = new StringBuilder();
        var buffer = new char[1024];
        int count;
        while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) != 0)
        {
            var take = Math.Min(count, 4096 - result.Length);
            if (take > 0) result.Append(buffer, 0, take);
        }
        return result.ToString();
    }
}
