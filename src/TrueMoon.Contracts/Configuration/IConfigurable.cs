namespace TrueMoon.Configuration;

/// <summary>
/// Key-value storage for configuration entries
/// </summary>
public interface IConfigurable
{
    /// <summary>
    /// Checks that entry with given key is exist in current instance
    /// </summary>
    /// <param name="key">entry key</param>
    /// <returns>true if entry exist, false otherwise</returns>
    bool Exist(string key);
    
    /// <summary>
    /// Creates a new entry with given key and value or updates existed one
    /// </summary>
    /// <param name="key">key</param>
    /// <param name="value">value</param>
    /// <typeparam name="T">type of the value</typeparam>
    void Set<T>(string key, T? value);
    
    /// <summary>
    /// Get value of the entry by the given key
    /// <para>If entry with given key does not exist, will return null or default value/></para>
    /// </summary>
    /// <param name="key">key</param>
    /// <typeparam name="T">type of the value</typeparam>
    /// <returns>value if key is exist</returns>
    T? Get<T>(string key);

    /// <summary>
    /// Get value of the entry by the given key
    /// <para>If entry with given key does not exist, will return null or default value/></para>
    /// </summary>
    /// <param name="key">key</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <typeparam name="T">type of the value</typeparam>
    /// <returns>value if key is exist</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Try get value by given key
    /// </summary>
    /// <param name="key">key</param>
    /// <param name="value">value</param>
    /// <typeparam name="T">type of the value</typeparam>
    /// <returns>true of entry is exist, false otherwise</returns>
    bool TryGetValue<T>(string key, out T? value);

    IReadOnlyList<string> GetKeys();
    IReadOnlyList<object?> GetValues();
    IReadOnlyList<(string key, object? value)> GetList();
}

/// <summary>
/// Key-value storage for typed configuration entries
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IConfigurable<T> : IConfigurable
{
    void Configure(Action<T> action);
    void Configure(Func<T> action);
    void Configure(Func<T,T> action);    
    
    Task ConfigureAsync(Action<T> action, CancellationToken cancellationToken = default);
    Task ConfigureAsync(Func<T> action, CancellationToken cancellationToken = default);
    Task ConfigureAsync(Func<T,T> action, CancellationToken cancellationToken = default);
    Task ConfigureAsync(Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task ConfigureAsync(Func<T,Task<T>> action, CancellationToken cancellationToken = default);
    
    T? Get();
    ValueTask<T?> GetAsync(CancellationToken cancellationToken = default);
}