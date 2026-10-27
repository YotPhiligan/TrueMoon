using System.Diagnostics;
using System.Reflection;

namespace TrueMoon.Aether.Hosting;

/// <summary>Explicit, immutable command for an owned native runtime process. No process is started during configuration.</summary>
public sealed class NativeRuntimeLaunch
{
    /// <summary>Creates a launch descriptor; paths must be absolute and arguments are copied without shell parsing.</summary>
    /// <param name="executablePath">Apphost executable or dotnet executable.</param>
    /// <param name="runtimeAssemblyPath">Managed entry assembly actually executed by the child, used for exact build verification.</param>
    /// <param name="arguments">Individual arguments, including the runtime DLL when using dotnet. No implicit argument is added.</param>
    /// <param name="workingDirectory">Child working directory; null selects the runtime assembly directory.</param>
    public NativeRuntimeLaunch(string executablePath, string runtimeAssemblyPath, IEnumerable<string>? arguments = null, string? workingDirectory = null)
    {
        ExecutablePath = Absolute(executablePath, nameof(executablePath));
        RuntimeAssemblyPath = Absolute(runtimeAssemblyPath, nameof(runtimeAssemblyPath));
        WorkingDirectory = Absolute(workingDirectory ?? Path.GetDirectoryName(RuntimeAssemblyPath)!, nameof(workingDirectory));
        var copied = arguments?.ToArray() ?? [];
        if (copied.Length > 64 || copied.Any(value => value == null || value.Contains('\0')) || copied.Sum(value => (long)value.Length) > 16384)
            throw new ArgumentException("Runtime arguments require at most 64 non-null values and 16384 characters without NUL.", nameof(arguments));
        Arguments = Array.AsReadOnly(copied);
    }

    /// <summary>Absolute executable path; the current executable is never selected implicitly.</summary>
    public string ExecutablePath { get; }
    /// <summary>Absolute managed runtime entry assembly path, not the dotnet host or apphost.</summary>
    public string RuntimeAssemblyPath { get; }
    /// <summary>Copied individual process arguments.</summary>
    public IReadOnlyList<string> Arguments { get; }
    /// <summary>Absolute child working directory.</summary>
    public string WorkingDirectory { get; }

    /// <summary>Explicitly selects the current application as the runtime, for hosts implementing their own runtime branch.</summary>
    /// <param name="arguments">Explicit runtime selector and other arguments, for example --runtime.</param>
    /// <returns>A descriptor handling both apphost and dotnet entry assembly launch forms.</returns>
    public static NativeRuntimeLaunch CurrentProcess(params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The current executable path is unavailable.");
        var entry = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrEmpty(entry)) throw new NotSupportedException("A managed entry assembly is required for runtime build verification.");
        return new(executable, entry,
            string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase) ? [entry, .. arguments] : arguments,
            AppContext.BaseDirectory);
    }

    internal ProcessStartInfo StartInfo()
    {
        var info = new ProcessStartInfo(ExecutablePath)
        {
            WorkingDirectory = WorkingDirectory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in Arguments) info.ArgumentList.Add(argument);
        return info;
    }

    private static string Absolute(string path, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameter);
        if (!Path.IsPathFullyQualified(path) || path.Contains('\0')) throw new ArgumentException("An absolute runtime path without NUL is required.", parameter);
        return Path.GetFullPath(path);
    }
}
