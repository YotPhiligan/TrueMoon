namespace TrueMoon.Configuration;

/// <summary>Configuration with providers in descending priority order and atomic refresh snapshots.</summary>
public class CommonConfiguration : IConfiguration
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IConfigurationProvider[] _providers;
    private IConfigurationSection[] _sections = [];

    public CommonConfiguration(IEnumerable<IConfigurationProvider> configurationProviders)
    {
        ArgumentNullException.ThrowIfNull(configurationProviders);
        _providers = configurationProviders.ToArray();
        RefreshCore();
    }

    private IConfigurationSection? GetSectionCore(string? name)
    {
        name = string.IsNullOrWhiteSpace(name) ? ConfigurationSectionNames.Default : name;
        var matches = _sections.Where(section => section.Name == name).ToArray();
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => new OverlaySection(name, matches)
        };
    }

    public IConfigurationSection? GetSection(string? name = default)
    {
        _gate.Wait();
        try { return GetSectionCore(name); }
        finally { _gate.Release(); }
    }

    public async Task<IConfigurationSection?> GetSectionAsync(string? name = default, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return GetSectionCore(name); }
        finally { _gate.Release(); }
    }

    public bool TryGetSection(string? name, out IConfigurationSection? section)
    {
        section = GetSection(name);
        return section is not null;
    }

    public IConfigurationSection[] GetSections()
    {
        _gate.Wait();
        try { return _sections.ToArray(); }
        finally { _gate.Release(); }
    }

    public void Refresh()
    {
        _gate.Wait();
        try { RefreshCore(); }
        finally { _gate.Release(); }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { RefreshCore(); }
        finally { _gate.Release(); }
    }

    public object? this[string key]
    {
        get => this.Get<object>(key);
        set => this.Set(key, value);
    }

    public object? this[string key, string section]
    {
        get => this.Get<object>(key, section);
        set => this.Set(key, value, section);
    }

    private void RefreshCore()
    {
        // Construct fully before publishing: a broken provider does not erase the last valid snapshot.
        var sections = _providers.SelectMany(provider => provider.GetSections()).ToArray();
        _sections = sections;
    }

    private sealed class OverlaySection(string name, IConfigurationSection[] sections) : IConfigurationSection
    {
        public string Name => name;
        private IConfigurationSection? Find(string key) => sections.FirstOrDefault(section => section.Exist(key));
        public bool Exist(string key) => Find(key) is not null;
        public void Set<T>(string key, T? value) => sections[0].Set(key, value);
        public T? Get<T>(string key) => Find(key) is { } section ? section.Get<T>(key) : default;
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Find(key) is { } section ? section.GetAsync<T>(key, cancellationToken) : Task.FromResult(default(T));
        }
        public bool TryGetValue<T>(string key, out T? value)
        {
            if (Find(key) is { } section) return section.TryGetValue(key, out value);
            value = default;
            return false;
        }
        public IReadOnlyList<(string key, object? value)> GetList() => sections.SelectMany(section => section.GetList())
            .DistinctBy(entry => entry.key).ToArray();
        public IReadOnlyList<string> GetKeys() => GetList().Select(entry => entry.key).ToArray();
        public IReadOnlyList<object?> GetValues() => GetList().Select(entry => entry.value).ToArray();
    }
}
