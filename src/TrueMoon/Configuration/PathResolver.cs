using System.Diagnostics;
using System.Reflection;

namespace TrueMoon.Configuration;

/// <summary>Resolves application folders. An exact -root= argument overrides the local product root.</summary>
public class PathResolver : IPathResolver
{
    private readonly string _rootPath;
    private readonly string _secrets;

    public PathResolver() : this(ConfigurationCommandLine.Capture()) { }

    /// <summary>Creates a resolver from arguments excluding the executable.</summary>
    public PathResolver(IEnumerable<string> arguments) : this(Parse(arguments)) { }

    /// <summary>Creates a resolver from a detached, validated startup path snapshot.</summary>
    /// <param name="settings">Product root settings; subsequent mutations do not affect this resolver.</param>
    public PathResolver(PathSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var (companyName, productName) = GetInfo();
        if (settings.Root is not null && string.IsNullOrWhiteSpace(settings.Root)) throw new ArgumentException("Invalid product root.", nameof(settings));
        _rootPath = settings.Root is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), companyName, productName)
            : Path.GetFullPath(settings.Root);
        _secrets = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), companyName, productName);
    }

    private static PathSettings Parse(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        try
        {
            var selected = ConfigurationCommandLine.ParseKnown(arguments.ToArray(), CoreConfigurationSchemas.Paths.Keys);
            var profile = ConfigurationProfiles.Create(builder =>
            {
                builder.AddSchema(CoreConfigurationSchemas.Paths);
                builder.AddValues("path-arguments", ConfigurationSourcePriority.Arguments, selected.Entries);
            });
            return CoreConfigurationSchemas.Paths.Bind(profile.LoadAsync().GetAwaiter().GetResult());
        }
        catch (ConfigurationException) { throw new ArgumentException("Invalid product root settings.", nameof(arguments)); }
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
