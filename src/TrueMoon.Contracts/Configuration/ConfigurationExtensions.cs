namespace TrueMoon.Configuration;

public static class ConfigurationExtensions
{
    public static IConfiguration Set<T>(this IConfiguration configuration, string key, T? value, string? sectionName = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(sectionName)
            ?? throw new InvalidOperationException($"section \"{sectionName}\" not found");
        section.Set(key, value);
        return configuration;
    }

    /// <summary>Reads the highest priority existing key. Invalid values do not fall back to lower providers.</summary>
    public static T? Get<T>(this IConfiguration configuration, string? key = null, string? sectionName = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        key ??= typeof(T).Name;
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            foreach (var section in configuration.GetSections())
                if (section.Exist(key)) return section.Get<T>(key);
            return default;
        }
        return configuration.GetSection(sectionName) is { } namedSection ? namedSection.Get<T>(key) : default;
    }

    public static bool Exist(this IConfiguration configuration, string key, string? sectionName = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return string.IsNullOrWhiteSpace(sectionName)
            ? configuration.GetSections().Any(section => section.Exist(key))
            : configuration.GetSection(sectionName)?.Exist(key) == true;
    }

    public static string? GetName(this IConfiguration configuration) => configuration.Get<string>("appName");

    public static T GetOrCreate<T>(this IConfiguration configuration, string? key = null, string? sectionName = null)
        where T : class, new()
    {
        key ??= typeof(T).Name;
        var item = configuration.Get<T>(key, sectionName);
        if (item is not null) return item;
        item = new T();
        configuration.Set(key, item, sectionName);
        return item;
    }
}
