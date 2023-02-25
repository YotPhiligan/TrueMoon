namespace TrueMoon.Configuration;

public static class ConfigurationExtensions
{
    public static IConfiguration Set<T>(this IConfiguration configuration, string key, T? value, string? sectionName = default)
    {
        var section = configuration.GetSection(sectionName);
        if (section is null)
        {
            throw new InvalidOperationException($"section \"{sectionName}\" not found");
        }
        section.Set(key, value);
        return configuration;
    }
    
    public static T? Get<T>(this IConfiguration configuration, string key, string? sectionName = default)
    {
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
}