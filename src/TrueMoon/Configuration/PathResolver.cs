using System.Diagnostics;
using System.Reflection;

namespace TrueMoon.Configuration;

/// <summary>Resolves application folders. An exact -root= argument overrides the local product root.</summary>
public class PathResolver : IPathResolver
{
    private readonly string _rootPath;
    private readonly string _secrets;

    public PathResolver() : this(Environment.GetCommandLineArgs().Skip(1)) { }

    /// <summary>Creates a resolver from arguments excluding the executable.</summary>
    public PathResolver(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var (companyName, productName) = GetInfo();
        var argument = arguments.LastOrDefault(value => value.StartsWith("-root=", StringComparison.Ordinal));
        if (argument is not null && string.IsNullOrWhiteSpace(argument[6..]))
            throw new ArgumentException("The -root= argument must contain a path.", nameof(arguments));
        _rootPath = argument is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), companyName, productName)
            : Path.GetFullPath(argument[6..]);
        _secrets = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), companyName, productName);
    }

    private static (string companyName, string productName) GetInfo()
    {
        // The entry assembly describes the app; Environment.ProcessPath may be the dotnet host.
        var assembly = Assembly.GetEntryAssembly();
        var location = assembly?.Location;
        var info = string.IsNullOrWhiteSpace(location) ? null : FileVersionInfo.GetVersionInfo(location);
        var company = assembly?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? info?.CompanyName ?? "";
        var product = assembly?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? info?.ProductName;
        if (string.IsNullOrWhiteSpace(product)) product = assembly?.GetName().Name ?? Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "TrueMoon";
        return (company, product);
    }

    public string ResolvePath(Paths path) => path switch
    {
        Paths.Root => _rootPath,
        Paths.Assets => Path.Combine(_rootPath, nameof(Paths.Assets)),
        Paths.Configuration => Path.Combine(_rootPath, nameof(Paths.Configuration)),
        Paths.Data => Path.Combine(_rootPath, nameof(Paths.Data)),
        Paths.Logs => Path.Combine(_rootPath, nameof(Paths.Logs)),
        Paths.Secrets => _secrets,
        _ => _rootPath
    };
}
