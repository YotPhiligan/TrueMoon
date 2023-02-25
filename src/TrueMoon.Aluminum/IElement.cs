namespace TrueMoon.Aluminum;

public interface IElement
{
    IElement? Parent { get; }
    void SetParent(IElement? element);
    
    IParameter<T>? Get<T>(string name);
    void Add<T>(IParameter<T> parameter);
    void Set<T>(string name, T? value);
    void Set<T>(T? value);
    bool Has(string name);
    bool Has<T>();
    bool TryGet<T>(string name, out IParameter<T>? parameter);
}

public interface IElementContainer : IReadOnlyList<IElement>, IElement
{
    void Add(IElement element);
    void Remove(IElement element);
}