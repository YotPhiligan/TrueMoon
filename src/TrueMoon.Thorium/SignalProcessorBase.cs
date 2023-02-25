using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using TrueMoon.Thorium.IO;
using TrueMoon.Thorium.Signals;
using TrueMoon.Thorium.Utils;

namespace TrueMoon.Thorium;

public abstract class SignalProcessorBase : IDisposable
{
    protected readonly string Name;

    protected SignalProcessorBase(string name)
    {
        Name = name;
        Initialize();
    }

    private void Initialize()
    {
        //TODO better unix approach
        MemoryMappedFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
            ? MemoryMappedFile.OpenExisting(Name, MemoryMappedFileRights.ReadWrite)
            : MemoryMappedFile.CreateFromFile(Name, FileMode.Open);
        Accessor = MemoryMappedFile.CreateViewAccessor();
    }

    protected MemoryMappedFile MemoryMappedFile;
    protected MemoryMappedViewAccessor Accessor;

    public void Dispose()
    {
        try
        {
            ReleaseResources();
        }
        catch (Exception)
        {
            //
        }
        Accessor.Dispose();
        MemoryMappedFile.Dispose();
    }

    protected virtual void ReleaseResources()
    {
        
    }


    protected MemoryReadHandle GetReadHandle(int offset, int size)
    {
        var view = MemoryMappedFile.CreateViewStream(offset, size);

        return new MemoryReadHandle(view);
    }

    protected SignalStorageDescriptor GetStorageDetails()
    {
        var details = SignalStorageUtils.GetDescriptor(Accessor);
        return details;
    }
}