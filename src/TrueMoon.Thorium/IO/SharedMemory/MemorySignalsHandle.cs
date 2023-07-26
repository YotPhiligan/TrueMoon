using System.IO.MemoryMappedFiles;
using System.Reflection;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.IO.SharedMemory.Utils;
using TrueMoon.Thorium.IO.Signals;

namespace TrueMoon.Thorium.IO.SharedMemory;

public class MemorySignalsHandle<T> : MemorySignalsHandle, ISignalsHandle<T>
{
    public MemorySignalsHandle(ThoriumConfiguration configuration, IEventsSource<MemorySignalsHandle<T>> eventsSource) : base(typeof(T).FullName!, configuration, eventsSource)
    {
    }
}

public class MemorySignalsHandle : ISignalsHandle, IDisposable
{
    private readonly ThoriumConfiguration _configuration;
    private readonly IEventsSource _eventsSource;
    private MemoryMappedFile? _memoryFile;

    public MemorySignalsHandle(string name, ThoriumConfiguration configuration, IEventsSource eventsSource)
    {
        Name = $"tm_{name}";
        _configuration = configuration;
        _eventsSource = eventsSource;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            var name = Name;
            
            var smallSignals = _configuration.SmallSignals;
            var smallSignalSize = _configuration.SmallSignalSize;
            var largeSignals = _configuration.LargeSignals;
            var largeSignalSize = _configuration.LargeSignalSize;


            var size = smallSignals * smallSignalSize + largeSignals * largeSignalSize;

            var descSize = SignalStorageUtils.DescriptorSize;

            var capacity = descSize + size;
            
            _memoryFile = MemoryMappedFile.CreateNew(name, capacity, MemoryMappedFileAccess.ReadWrite);
        
            using var ms = _memoryFile.CreateViewAccessor();

            var descriptor = new SignalStorageDescriptor(smallSignals, smallSignalSize, largeSignals, largeSignalSize);
            
            ms.Write(0, ref descriptor);

            int offset = descSize;
            for (byte i = 0; i < smallSignals; i++)
            {
                var signal = new Signal(i, SignalType.None, SignalStatus.None, 0, Guid.Empty, 0, SignalLocation.Small,
                    SignalLocation.Small, 0, 0);
                
                ms.Write(offset, ref signal);
                offset += smallSignalSize;
            }

            for (byte i = 0; i < largeSignals; i++)
            {
                var signal = new Signal(i, SignalType.None, SignalStatus.None, 0, Guid.Empty, 0, SignalLocation.Large,
                    SignalLocation.Large, 0, 0);
                
                ms.Write(offset, ref signal);
                offset += largeSignalSize;
            }

            ms.Flush();

            _eventsSource.Write(()=>Name);
        }
        catch (Exception e)
        {
            _eventsSource.Exception(e);
            throw;
        }
    }

    public string Name { get; }
    public bool IsAlive => _memoryFile is { };

    public SignalStorageDescriptor GetDescriptor()
    {
        if (!IsAlive)
        {
            throw new InvalidOperationException("handle is not initialized");
        }
        
        using var ms = _memoryFile.CreateViewAccessor();

        return SignalStorageUtils.GetDescriptor(ms);
    }
    
    public void Dispose()
    {
        _memoryFile?.Dispose();

        _memoryFile = null;
    }
}