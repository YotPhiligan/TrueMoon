namespace TrueMoon.Configuration;

public static class ConfigurationExtensions
{
    public static IConfiguration Set<T>(this IConfiguration configuration, string key, T? value, string? sectionName = null)
    {
        var section = configuration.GetSection(sectionName);
        if (section is null)
        {
            throw new InvalidOperationException($"section \"{sectionName}\" not found");
        }
        
        section.Set(key, value);
        return configuration;
    }
    
    public static T? Get<T>(this IConfiguration configuration, string? key = null, string? sectionName = null)
    {
        key ??= typeof(T).Name;
        
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            foreach (var configurationSection in configuration.GetSections())
            {
                if (configurationSection.TryGetValue<T>(key, out var value))
                {
                    return value;
                }
            }
        }
        
        var section = configuration.GetSection(sectionName);
        return section != null ? section.Get<T>(key) : default;
    }

    public static bool Exist(this IConfiguration configuration, string key, string? sectionName = default)
    {
        var section = configuration.GetSection(sectionName);
        return section != null && section.Exist(key);
    }
    
    public static string? GetName(this IConfiguration configuration)
    {
        var section = configuration.GetSection();

        return section?.Get<string>("appName");
    }
    
    public static T GetOrCreate<T>(this IConfiguration configuration, string? key = null, string? sectionName = null)
        where T : class, new()
    {
        var item = configuration.Get<T>(key,sectionName);
        if (item != null)
        {
            return item;
        }
        
        item ??= new T();
        configuration.Set(key, item, sectionName);
        return item;
    }
}