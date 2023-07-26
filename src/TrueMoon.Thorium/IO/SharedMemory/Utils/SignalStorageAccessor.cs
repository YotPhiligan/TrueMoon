using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using TrueMoon.Thorium.IO.Signals;

namespace TrueMoon.Thorium.IO.SharedMemory.Utils;

public class SignalStorageAccessor : IDisposable
{
    private MemoryMappedFile? _memoryFile;
    private readonly string _fileName;

    public SignalStorageAccessor(string name)
    {
        _fileName = name;
    }
    
    public void Write(ReadOnlySpan<byte> data, int offset = default, int? size = default)
    {
        var file = GetFile();
        using var ms = file.CreateViewStream(offset, size ?? data.Length);
        ms.Write(data);
    }
    
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, int offset = default, int? size = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var file = GetFile();
        await using var ms = file.CreateViewStream(offset, size ?? data.Length);
        await ms.WriteAsync(data, cancellationToken);
    }
    
    public int Read(Span<byte> data, int offset = default, int? size = default)
    {
        var file = GetFile();
        
        using var view = file.CreateViewStream(offset, size ?? data.Length);

        // try
        // {
        //     unsafe
        //     {
        //         byte* ptr = null;
        //         view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        //     }
        // }
        // finally
        // {
        //     view.SafeMemoryMappedViewHandle.ReleasePointer();
        // }

        var readLenght = view.Read(data);
        return readLenght;
    }
    
    public async ValueTask<int> ReadAsync(Memory<byte> data, int offset = default, int? size = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var file = GetFile();

        await using var view = file.CreateViewStream(offset, size ?? data.Length);

        var readLenght = await view.ReadAsync(data, cancellationToken);
        return readLenght;
    }

    public MemoryReadHandle GetReadHandle(int offset, int size)
    {
        var file = GetFile();

        var view = file.CreateViewStream(offset, size);

        return new MemoryReadHandle(view);
    }
    
    private MemoryMappedFile GetFile()
    {
        if (_memoryFile != null)
        {
            return _memoryFile;
        }

        try
        {
            //TODO better unix approach
            _memoryFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? MemoryMappedFile.OpenExisting(_fileName, MemoryMappedFileRights.ReadWrite)
                : MemoryMappedFile.CreateFromFile(_fileName, FileMode.Open);

            return _memoryFile;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException();
        }
    }

    public void Dispose()
    {
        _memoryFile?.Dispose();
    }

    public Signal GetSignal(int i, SignalLocation location = SignalLocation.Small)
    {
        var file = GetFile();

        var signalDesSize = SignalStorageUtils.SignalSize;
        var descriptorSize = SignalStorageUtils.DescriptorSize;
        
        using var view = file.CreateViewAccessor();

        var descriptor = SignalStorageUtils.GetDescriptor(view);
        
        var signalSize = location switch
        {
            SignalLocation.Small => descriptor.SmallSignalSize,
            SignalLocation.Large => descriptor.LargeSignalSize,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        var baseOffset = location switch
        {
            SignalLocation.Small => descriptorSize,
            SignalLocation.Large => descriptorSize+descriptor.SmallSignalSize*descriptor.SmallSignals,
            _ => throw new ArgumentOutOfRangeException()
        };

        var offset = baseOffset + i * signalSize;
        
        view.Read<Signal>(offset, out var signal);

        return signal;
    }
    
    public void ReadSignalPayload(Signal signal, Span<byte> span)
    {
        var file = GetFile();

        var signalDesSize = SignalStorageUtils.SignalSize;
        var descriptorSize = SignalStorageUtils.DescriptorSize;
        
        using var view = file.CreateViewAccessor();

        var descriptor = SignalStorageUtils.GetDescriptor(view);
        
        var signalSize = signal.Location switch
        {
            SignalLocation.Small => descriptor.SmallSignalSize,
            SignalLocation.Large => descriptor.LargeSignalSize,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        var baseOffset = signal.Location switch
        {
            SignalLocation.Small => descriptorSize,
            SignalLocation.Large => descriptorSize+descriptor.SmallSignalSize*descriptor.SmallSignals,
            _ => throw new ArgumentOutOfRangeException()
        };

        var offset = baseOffset + signal.Index * signalSize + signalDesSize;
        view.SafeMemoryMappedViewHandle.ReadSpan((ulong)offset, span);
        
    }
}