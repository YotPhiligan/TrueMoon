// using TrueMoon.Diagnostics;
// using TrueMoon.Thorium.IO.SharedMemory;
//
// namespace TrueMoon.Thorium.IO.MemoryMappedFiles;
//
// public class MemoryMappedFileSignalsHandleFactory : ISignalsHandleFactory
// {
//     private readonly ThoriumConfiguration _thoriumConfiguration;
//     private readonly IEventsSourceFactory _eventsSourceFactory;
//
//     public MemoryMappedFileSignalsHandleFactory(ThoriumConfiguration thoriumConfiguration,
//         IEventsSourceFactory eventsSourceFactory)
//     {
//         _thoriumConfiguration = thoriumConfiguration;
//         _eventsSourceFactory = eventsSourceFactory;
//     }
//     
//     public ISignalsHandle<T> Create<T>() 
//         => new MemoryMappedFileSignalsHandle<T>(_thoriumConfiguration, _eventsSourceFactory.Create<MemoryMappedFileSignalsHandle<T>>());
// }