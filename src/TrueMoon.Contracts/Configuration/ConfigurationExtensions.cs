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

    /// <summary>Returns an existing object or creates one in the section that supplied a null value.
    /// A missing unqualified key is created in the default section.</summary>
    /// <param name="configuration">Configuration to read and update.</param>
    /// <param name="key">Entry key; defaults to the type name.</param>
    /// <param name="sectionName">Optional section name; otherwise searches in provider priority order.</param>
    /// <typeparam name="T">Configuration object type.</typeparam>
    /// <returns>The existing or newly stored object.</returns>
    public static T GetOrCreate<T>(this IConfiguration configuration, string? key = null, string? sectionName = null)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        key ??= typeof(T).Name;
        var source = string.IsNullOrWhiteSpace(sectionName)
            ? configuration.GetSections().FirstOrDefault(section => section.Exist(key))
            : configuration.GetSection(sectionName);
        var item = source is null ? default : source.Get<T>(key);
        if (item is not null) return item;
        item = new T();
        if (source is not null) source.Set(key, item);
        else configuration.Set(key, item, sectionName);
        return item;
    }
}
