using System.IO.MemoryMappedFiles;
using System.Reflection;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.Signals;
using TrueMoon.Thorium.Utils;

namespace TrueMoon.Thorium;

public class StorageHandle : IDisposable
{
    private readonly ThoriumConfiguration _configuration;
    private readonly IEventsSource<StorageHandle> _eventsSource;
    private MemoryMappedFile? _memoryFile;
    private Mutex? _writeMutex;

    public StorageHandle(ThoriumConfiguration configuration, IEventsSource<StorageHandle> eventsSource)
    {
        _configuration = configuration;
        _eventsSource = eventsSource;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            var name = string.IsNullOrWhiteSpace(_configuration.Name)
                ? $"tm_{Assembly.GetEntryAssembly()?.GetName().Name}"
                : _configuration.Name;
            
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
                var signal = new Signal(i, SignalType.None, SignalStatus.None, Guid.Empty, 0, SignalLocation.Small,
                    SignalLocation.Small, 0, 0);
                
                ms.Write(offset, ref signal);
                offset += smallSignalSize;
            }

            for (byte i = 0; i < largeSignals; i++)
            {
                var signal = new Signal(i, SignalType.None, SignalStatus.None, Guid.Empty, 0, SignalLocation.Large,
                    SignalLocation.Large, 0, 0);
                
                ms.Write(offset, ref signal);
                offset += largeSignalSize;
            }

            ms.Flush();
            
            _writeMutex = new Mutex(false, $"Global\\{name}_mw");
            
            _eventsSource.Write(()=>Name);
        }
        catch (Exception e)
        {
            _eventsSource.Write(()=>e);
            throw;
        }
    }

    public string Name => _configuration.Name;
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
        _writeMutex?.Dispose();

        _memoryFile = null;
    }
}