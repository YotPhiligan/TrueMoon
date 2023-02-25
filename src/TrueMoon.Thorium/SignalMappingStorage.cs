using System.Collections.Concurrent;
using System.Runtime.Loader;
using System.Text;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.Signals;

namespace TrueMoon.Thorium;

public class SignalMappingStorage
{
    private readonly IEventsSource _eventsSource;
    private readonly ConcurrentDictionary<Guid,SignalMappingItem> _items = new ();
    private readonly List<Type> _listenMessages = new ();
    private readonly ConcurrentDictionary<Type,Guid> _types = new ();

    public SignalMappingStorage(IEventsSource eventsSource)
    {
        _eventsSource = eventsSource;
    }

    public void Register<TMessage>()
    {
        _listenMessages.Add(typeof(TMessage));
    }

    public Type? GetHandlerType(Guid code, SignalType signalType) 
        => _items.TryGetValue(code, out var value) 
            ? signalType switch {
                SignalType.Request => value.SignalReponseWrapperType,
                _ => value.SignalWrapperType
            } 
            : default;

    public Guid[] GetRegisteredCodes()
    {
        var result = _items
            .Values
            .Where(t => t.IsListen is true)
            .Select(t => t.Code)
            .ToArray();
        
        return result;
    }

    public Guid GetCode<TMessage>()
    {
        var item = _types.TryGetValue(typeof(TMessage), out var v) ? v : default;
        if (item == Guid.Empty)
        {
            throw new InvalidOperationException($"{typeof(TMessage)} not found");
        }
        return item;
    }

    private bool _isInitialized;
    
    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }
        
        _isInitialized = true;
        
        var types = AssemblyLoadContext
            .Default
            .Assemblies
            .SelectMany(t=>t.GetTypes())
            .Where(t => typeof(ISignalMapping).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .ToList();
        
        foreach (var type in types)
        {
            if (Activator.CreateInstance(type) is not ISignalMapping instance) continue;
            
            var values = instance.GetValues();

            foreach (var value in values)
            {
                _items.TryAdd(value.Code, value);
                _types.TryAdd(value.SignalType, value.Code);
            }
        }

        foreach (var listenMessage in _listenMessages)
        {
            var v = _items.Values.FirstOrDefault(t => t.SignalType == listenMessage);
            if (v != null)
            {
                v.IsListen = true;
            }
            else
            {
                throw new InvalidOperationException($"{listenMessage} not registered");
            }
            
        }

        _eventsSource.Write(() =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("Messages:");
            foreach (var item in _items.Values)
            {
                sb.AppendLine($"    -  {item.FullName} ({item.Code}) : {item.IsListen}");
            }

            return sb;
        }, $"{nameof(SignalMappingStorage)}.Initialized");
    }
}