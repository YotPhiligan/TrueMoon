namespace TrueMoon.Configuration;

public interface IConfiguration
{
    IConfigurationSection? GetSection(string? name = default);
    Task<IConfigurationSection?> GetSectionAsync(string? name = default, CancellationToken cancellationToken = default);
    bool TryGetSection(string? name, out IConfigurationSection? section);
    
    object? this[string key] { get; set; }
    object? this[string key, string section] { get; set; }
}