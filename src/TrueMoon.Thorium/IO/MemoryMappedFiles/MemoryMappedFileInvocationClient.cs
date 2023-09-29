// using System.Buffers;
// using System.Collections.Concurrent;
// using TrueMoon.Diagnostics;
//
// namespace TrueMoon.Thorium.IO.MemoryMappedFiles;
//
// public class MemoryMappedFileInvocationClient<T> : IInvocationClient<T>, IDisposable
// {
//     private readonly IEventsSource<MemoryMappedFileInvocationClient<T>> _eventsSource;
//     private readonly ThoriumConfiguration _configuration;
//
//     public MemoryMappedFileInvocationClient(IEventsSource<MemoryMappedFileInvocationClient<T>> eventsSource, ThoriumConfiguration configuration)
//     {
//         _eventsSource = eventsSource;
//         _configuration = configuration;
//     }
//
//     private readonly ConcurrentDictionary<int, MemoryMappedFileSignalClientChannel> _channels = new (); 
//     
//     public Task<TResult> InvokeAsync<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<IMemoryReadHandle, TResult> func, CancellationToken cancellationToken = default)
//     {
//         CheckChannel(methodCode);
//         return _channels[methodCode].InvokeAsync(action, func, cancellationToken);
//     }
//
//     private void CheckChannel(byte methodCode)
//     {
//         if (!_channels.ContainsKey(methodCode))
//         {
//             CreateChannel(methodCode);
//         }
//     }
//
//     public Task InvokeAsync(byte methodCode, Action<IBufferWriter<byte>>? action, CancellationToken cancellationToken = default)
//     {
//         CheckChannel(methodCode);
//         return _channels[methodCode].InvokeAsync(action, cancellationToken);
//     }
//
//     public TResult Invoke<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<IMemoryReadHandle, TResult> func)
//     {
//         CheckChannel(methodCode);
//         return _channels[methodCode].Invoke(action, func);
//     }
//
//     public void Invoke(byte methodCode, Action<IBufferWriter<byte>>? action)
//     {
//         CheckChannel(methodCode);
//         _channels[methodCode].Invoke(action);
//     }
//
//     private void CreateChannel(byte methodCode)
//     {
//         var channel =
//             new MemoryMappedFileSignalClientChannel(methodCode, $"tm_{typeof(T).FullName}", _eventsSource, _configuration);
//
//         _channels.TryAdd(methodCode, channel);
//     }
//
//     public void Dispose()
//     {
//         foreach (var channel in _channels.Values)
//         {
//             channel.Dispose();
//         }
//     }
// }