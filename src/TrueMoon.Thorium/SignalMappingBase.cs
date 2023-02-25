namespace TrueMoon.Thorium;

public abstract class SignalMappingBase : ISignalMapping
{
    protected readonly List<SignalMappingItem> Mappings = new ();
    
    protected SignalMappingBase(string assemblyName)
    {
        AssemblyName = assemblyName;
    }

    protected void AddMapping<T>(string name, string fullname, Guid code)
    {
        if (Mappings.Any(t=>t.FullName == fullname))
        {
            return;
        }

        Mappings.Add(new SignalMappingItem
        {
            Name = name,
            FullName = fullname,
            SignalType = typeof(T),
            SignalWrapperType = typeof(ISignalHandleWrapper<T>),
            Code = code
        });
    }
    
    protected void AddMapping<T,TResponse>(string name, string fullname, Guid code)
    {
        if (Mappings.Any(t=>t.FullName == fullname))
        {
            return;
        }

        Mappings.Add(new SignalMappingItem
        {
            Name = name,
            FullName = fullname,
            SignalType = typeof(T),
            SignalWrapperType = typeof(ISignalHandleWrapper<T>),
            SignalReponseWrapperType = typeof(ISignalHandleWrapper<T,TResponse>),
            Code = code
        });
    }

    public string AssemblyName { get; }
    
    public IReadOnlyList<SignalMappingItem> GetValues() => Mappings;
}