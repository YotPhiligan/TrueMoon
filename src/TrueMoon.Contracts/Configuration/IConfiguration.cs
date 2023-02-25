namespace TrueMoon.Configuration;

public interface IConfiguration
{
    IConfigurationSection? GetSection(string? name = default);
    Task<IConfigurationSection?> GetSectionAsync(string? name = default, CancellationToken cancellationToken = default);
    bool TryGetSection(string? name, out IConfigurationSection? section);
    void AddProvider(IConfigurationProvider configurationProvider);
    void RemoveProvider<T>(T? configurationProvider = default) where T : IConfigurationProvider;
    void Refresh();
    Task RefreshAsync(CancellationToken cancellationToken = default);
    
    object? this[string key] { get; set; }
    object? this[string key, string section] { get; set; }
}