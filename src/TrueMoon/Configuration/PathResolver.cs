using System.Diagnostics;

namespace TrueMoon.Configuration;

public class PathResolver : IPathResolver
{
    private readonly string _rootPath;
    private readonly string _secrets;

    public PathResolver()
    {
        _rootPath = GetRoot();
        _secrets = GetSecrets();
    }

    private static string GetRoot()
    {
        var args = Environment.GetCommandLineArgs();

        var rootsArg = args.FirstOrDefault(t => t.Contains("-root="));
        
        if (!string.IsNullOrWhiteSpace(rootsArg))
        {
            return Path.GetFullPath(rootsArg);
        }
        
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var (companyName, productName) = GetInfo();

        var result = Path.Combine(appDataPath, $"{companyName}", $"{productName}");
        return result;
    }

    private static (string? companyName, string? productName) GetInfo()
    {
        var processPath = Environment.ProcessPath;
        var processName = Path.GetFileNameWithoutExtension(processPath);

        var info = FileVersionInfo.GetVersionInfo(processPath);
        var companyName = info.CompanyName;
        var productName = info.ProductName ?? processName;
        return (companyName,processName);
    }

    private static string GetSecrets()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var (companyName, productName) = GetInfo();

        var result = Path.Combine(appDataPath, $"{companyName}", $"{productName}");
        return result;
    }

    public string ResolvePath(Paths path)
    {
        var result = path switch {
            Paths.Root => _rootPath,
            Paths.Assets => Path.Combine(_rootPath, $"{Paths.Assets}"),
            Paths.Configuration => Path.Combine(_rootPath, $"{Paths.Configuration}"),
            Paths.Data => Path.Combine(_rootPath, $"{Paths.Data}"),
            Paths.Logs => Path.Combine(_rootPath, $"{Paths.Logs}"),
            Paths.Secrets => _secrets,
            _ => _rootPath
        };

        // TODO check access
        
        return result;
    }
}