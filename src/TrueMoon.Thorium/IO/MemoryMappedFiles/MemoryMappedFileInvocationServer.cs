// using System.Collections.Concurrent;
// using TrueMoon.Diagnostics;
//
// namespace TrueMoon.Thorium.IO.MemoryMappedFiles;
//
// public class MemoryMappedFileInvocationServer<T> : IInvocationServer<T>, IDisposable
// {
//     private readonly IEventsSource _eventsSource;
//     private readonly IInvocationServerHandler<T> _handler;
//     private readonly ThoriumConfiguration _configuration;
//
//     private bool _isInitialized;
//
//     private readonly ConcurrentDictionary<int, MemoryMappedFileSignalServerChannel> _channels = new ();
//
//     public MemoryMappedFileInvocationServer(IEventsSource<MemoryMappedFileInvocationServer<T>> eventsSource, IInvocationServerHandler<T> handler, ThoriumConfiguration configuration)
//     {
//         _eventsSource = eventsSource;
//         _handler = handler;
//         _configuration = configuration;
//         Listen();
//     }
//
//     private void Listen()
//     {
//         if (_isInitialized)
//         {
//             return;
//         }
//
//         var type = typeof(T);
//         var codes = type.GetMethods().Length;
//
//         for (int i = 0; i < codes; i++)
//         {
//             _channels.TryAdd(i, new MemoryMappedFileSignalServerChannel($"tm_{type.FullName}", (byte)i, _eventsSource, _handler, _configuration));
//         }
//
//         _isInitialized = true;
//     }
//
//     public string Id => typeof(T).FullName!;
//
//     public void Dispose()
//     {
//         foreach (var channel in _channels.Values)
//         {
//             channel.Dispose();
//         }
//     }
// }