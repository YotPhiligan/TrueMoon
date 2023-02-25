using System.Reflection;

namespace TrueMoon.Configuration;

/// <summary>
/// Json Configuration provider
/// </summary>
public class JsonConfigurationProvider : IConfigurationProvider
{
    private readonly IPathResolver _pathResolver;

    /// <summary>
    /// Create a new instance
    /// </summary>
    /// <param name="pathResolver"></param>
    public JsonConfigurationProvider(IPathResolver pathResolver)
    {
        _pathResolver = pathResolver;
        Name = "json";
    }

    /// <inheritdoc />
    public string Name { get; }

    private readonly Dictionary<string, IConfigurationSection> _sections = new ();

    /// <inheritdoc />
    public IReadOnlyList<IConfigurationSection> GetSections()
    {
        try
        {
            var directory = _pathResolver.ResolvePath(Paths.Configuration);
            if (!Directory.Exists(directory))
            {
                return new List<IConfigurationSection>();
            }

            var files = Directory.GetFiles(directory, "*.json", searchOption:SearchOption.TopDirectoryOnly);

            var sections = files
                .Select(path=>(Path.GetFileNameWithoutExtension(path), path))
                .Where(t=>!_sections.ContainsKey(t.Item1))
                .Select(t => new JsonConfigurationSection(t.path))
                .ToList();
            
            foreach (var section in sections)
            {
                _sections.Add(section.Name, section);
            }
            
            return _sections.Values.ToList();
        }
        catch (Exception e)
        {
            return new List<IConfigurationSection>();
        }
    }
}