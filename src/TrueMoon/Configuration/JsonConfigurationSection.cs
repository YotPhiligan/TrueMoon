namespace TrueMoon.Configuration;

public class JsonConfigurationSection : IConfigurationSection, IConfigurationFileHandle
{
    private readonly string _filePath;

    public JsonConfigurationSection(string filePath)
    {
        _filePath = filePath;
        Name = Path.GetFileNameWithoutExtension(_filePath);
    }

    public bool Exist(string key)
    {
        throw new NotImplementedException();
    }

    public void Set<T>(string key, T? value)
    {
        throw new NotImplementedException();
    }

    public T? Get<T>(string key)
    {
        throw new NotImplementedException();
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<string> GetKeys()
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<object?> GetValues()
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<(string key, object? value)> GetList()
    {
        throw new NotImplementedException();
    }

    public string Name { get; }
    
    public FileInfo GetFileInfo()
    {
        return new FileInfo(_filePath);
    }
}