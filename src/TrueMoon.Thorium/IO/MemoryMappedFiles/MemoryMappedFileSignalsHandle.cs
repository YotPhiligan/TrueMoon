// using System.IO.MemoryMappedFiles;
// using System.Runtime.CompilerServices;
// using TrueMoon.Diagnostics;
//
// namespace TrueMoon.Thorium.IO.MemoryMappedFiles;
//
// public class MemoryMappedFileSignalsHandle<T> : MemoryMappedFileSignalsHandle, ISignalsHandle<T>
// {
//     public MemoryMappedFileSignalsHandle(ThoriumConfiguration configuration, IEventsSource<MemoryMappedFileSignalsHandle<T>> eventsSource) : base(typeof(T), configuration, eventsSource)
//     {
//     }
// }
//
// public class MemoryMappedFileSignalsHandle : ISignalsHandle, IDisposable
// {
//     private readonly ThoriumConfiguration _configuration;
//     private readonly IEventsSource _eventsSource;
//     private MemoryMappedFile? _memoryFile;
//
//     public MemoryMappedFileSignalsHandle(Type type, ThoriumConfiguration configuration, IEventsSource eventsSource)
//     {
//         Name = $"tm_{type.FullName}";
//         _configuration = configuration;
//         _eventsSource = eventsSource;
//         Initialize(type);
//     }
//
//     private void Initialize(Type type)
//     {
//         try
//         {
//             var name = Name;
//             
//             var codes = type.GetMethods().Length;
//
//             var capacity = (Unsafe.SizeOf<InvocationPackage>() + _configuration.LargeSignalSize) * codes;
//             
//             _memoryFile = MemoryMappedFile.CreateNew(name, capacity, MemoryMappedFileAccess.ReadWrite);
//
//             _eventsSource.Write(()=>$"{Name}, size: {capacity}");
//         }
//         catch (Exception e)
//         {
//             _eventsSource.Exception(e);
//             throw;
//         }
//     }
//
//     public string Name { get; }
//     public bool IsAlive => _memoryFile is { };
//     
//     public void Dispose()
//     {
//         _memoryFile?.Dispose();
//
//         _memoryFile = null;
//     }
// }