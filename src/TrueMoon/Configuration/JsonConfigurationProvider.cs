namespace TrueMoon.Configuration;

/// <summary>Loads one section per JSON file. Read and parse errors propagate to the caller.</summary>
public class JsonConfigurationProvider : IConfigurationProvider
{
    private readonly IPathResolver _pathResolver;

    public JsonConfigurationProvider(IPathResolver pathResolver)
    {
        ArgumentNullException.ThrowIfNull(pathResolver);
        _pathResolver = pathResolver;
    }

    public string Name => "json";

    public IReadOnlyList<IConfigurationSection> GetSections()
    {
        var directory = _pathResolver.ResolvePath(Paths.Configuration);
        if (!Directory.Exists(directory)) return [];
        return Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(path => (IConfigurationSection)new JsonConfigurationSection(path)).ToArray();
    }
}
