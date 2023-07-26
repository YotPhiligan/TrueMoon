// using System.Buffers;
// using System.Collections.Concurrent;
//
// namespace TrueMoon.Thorium.IO.Pipes;
//
// public class SignalClient<T> : PipeConectionHandler, ISignalClient<T>
// {
//     public SignalClient() : base(typeof(T).FullName!)
//     {
//     }
//     
//     private readonly ConcurrentDictionary<Guid, TaskCompletionSource<(byte code, Memory<byte> payload)>> _responses = new ();
//
//     public async Task InvokeAsync(Guid guid, ReadOnlyMemory<byte> memory, CancellationToken cancellationToken = default)
//     {
//         try
//         {
//             var (code, payload) = await SendCoreAsync(guid, memory, cancellationToken);
//             
//             payload.Return();
//             
//             if (code != 0)
//             {
//                 throw new InvalidOperationException("Failed to execute");
//             }
//         }
//         finally
//         {
//             _responses.TryRemove(guid, out _);
//         }
//     }
//     
//     public async Task<Memory<byte>> RequestAsync(Guid guid, ReadOnlyMemory<byte> memory, CancellationToken cancellationToken = default)
//     {
//         try
//         {
//             var (code, payload) = await SendCoreAsync(guid, memory, cancellationToken);
//
//             if (code != 0)
//             {
//                 throw new InvalidOperationException("Failed to execute");
//             }
//
//             return payload;
//         }
//         finally
//         {
//             _responses.TryRemove(guid, out _);
//         }
//     }
//
//     private async Task<(byte,Memory<byte>)> SendCoreAsync(Guid guid, ReadOnlyMemory<byte> memory, CancellationToken cancellationToken)
//     {
//         using var ctsTimer = new CancellationTokenSource(TimeSpan.FromSeconds(50));
//         using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctsTimer.Token, cancellationToken);
//         var token = cts.Token;
//
//         var tcs = new TaskCompletionSource<(byte code, Memory<byte> payload)>();
//         _responses.TryAdd(guid, tcs);
//         await WriteSync.WaitAsync(async () =>
//         {
//             await PipeStream.WriteAsync(memory, token);
//             await PipeStream.FlushAsync(token);
//         }, cancellationToken: token);
//
//         _ = Task.Factory.StartNew(async () =>
//             {
//                 while (!await ReadResponseAsync(guid, token))
//                 {
//                 }
//             }, token,
//             TaskCreationOptions.PreferFairness | TaskCreationOptions.RunContinuationsAsynchronously,
//             TaskScheduler.Default);
//
//         return await tcs.Task.WaitAsync(token);
//     }
//
//     private async Task<bool> ReadResponseAsync(Guid guid, CancellationToken cancellationToken = default)
//     {
//         byte resultCode;
//         int len;
//         Guid resultGuid = default;
//         
//         await ReadSync.WaitAsync(async () =>
//         {
//             (resultGuid,resultCode,len) = PipeStream.GetOutput();
//         
//             if (resultCode == 0)
//             {
//                 Memory<byte> mem = Memory<byte>.Empty;
//                 if (len > 0)
//                 {
//                     mem = ArrayPool<byte>.Shared.Rent(len).AsMemory(len);
//
//                     await PipeStream.ReadExactlyAsync(mem, cancellationToken);
//                 }
//
//                 if (_responses.TryGetValue(resultGuid, out var container))
//                 {
//                     container.TrySetResult((resultCode, mem));
//                 }
//             }
//         }, cancellationToken: cancellationToken);
//
//         return guid == resultGuid;
//     }
// }