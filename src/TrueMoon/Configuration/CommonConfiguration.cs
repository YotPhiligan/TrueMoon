namespace TrueMoon.Configuration;

/// <inheritdoc />
public class CommonConfiguration : IConfiguration
{
    private readonly SemaphoreSlim _semaphoreSlim = new (1);
    private readonly List<IConfigurationProvider> _configurationProviders;
    private List<IConfigurationSection>? _sections;

    /// <summary>
    /// Create a new instance of <see cref="Configuration"/>
    /// </summary>
    /// <param name="configurationProviders">configuration providers</param>
    public CommonConfiguration(IEnumerable<IConfigurationProvider> configurationProviders)
    {
        _configurationProviders = configurationProviders.ToList();
        RefreshCore();
    }

    private IConfigurationSection? GetSectionCore(string? name = default)
    {
        if (_sections == null || !_sections.Any())
        {
            RefreshCore();
        }
        
        name = string.IsNullOrWhiteSpace(name) ? ConfigurationSectionNames.Default : name;
        return _sections?.FirstOrDefault(t => t.Name == name);
    }

    /// <inheritdoc />
    public IConfigurationSection? GetSection(string? name = default)
    {
        _semaphoreSlim.Wait();
        try
        {
            return GetSectionCore(name);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IConfigurationSection?> GetSectionAsync(string? name = default, CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            return GetSectionCore(name);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <inheritdoc />
    public bool TryGetSection(string? name, out IConfigurationSection? section)
    {
        section = GetSection(name);
        return section != null;
    }

    /// <inheritdoc />
    public void Refresh()
    {
        _semaphoreSlim.Wait();
        try
        {
            RefreshCore();
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            RefreshCore();
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public object? this[string key]
    {
        get => this[key, ""];
        set => this[key, ""] = value;
    }

    public object? this[string key, string section]
    {
        get => GetSection(section)?.Get<object>(key);
        set => GetSection(section)?.Set(key, value);
    }

    private void RefreshCore()
    {
        _sections = _configurationProviders
            .SelectMany(provider => provider.GetSections())
            .ToList();
    }
}