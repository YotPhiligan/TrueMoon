// using System.Buffers;
// using System.Runtime.CompilerServices;
// using TrueMoon.Diagnostics;
// using TrueMoon.Thorium.IO.Signals;
// using TrueMoon.Threading;
//
// namespace TrueMoon.Thorium.IO.MemoryMappedFiles;
//
// public class MemoryMappedFileSignalClientChannel : MemoryMappedFileInvocationProcessorBase
// {
//     private readonly int _code;
//     private readonly IEventsSource _eventsSource;
//     private readonly ThoriumConfiguration _configuration;
//     private readonly int _sizeOfPackageHeader = Unsafe.SizeOf<InvocationPackage>();
//
//     public MemoryMappedFileSignalClientChannel(int code, string name, IEventsSource eventsSource, ThoriumConfiguration configuration) : base(name)
//     {
//         _code = code;
//         _eventsSource = eventsSource;
//         _configuration = configuration;
//         _taskScheduler = new TmTaskScheduler($"{name}_{_code}",2);
//         Initializer();
//     }
//
//     private void Initializer()
//     {
//         _packageOffset = (Unsafe.SizeOf<InvocationPackage>() + _configuration.LargeSignalSize) * _code;
//     }
//
//     private readonly object _writeSync = new ();
//     private int _packageOffset;
//     private readonly TmTaskScheduler _taskScheduler;
//
//     public Task<TResult> InvokeAsync<TResult>(Action<IBufferWriter<byte>>? action, Func<IMemoryReadHandle, TResult> func, CancellationToken cancellationToken = default) =>
//         Task.Factory.StartNew(() => InvokeCore(action, func, cancellationToken), cancellationToken,
//             TaskCreationOptions.PreferFairness, _taskScheduler);
//
//     public Task InvokeAsync(Action<IBufferWriter<byte>>? action, CancellationToken cancellationToken = default) =>
//         Task.Factory.StartNew(() => InvokeCore(action, cancellationToken), cancellationToken,
//             TaskCreationOptions.PreferFairness, _taskScheduler);
//
//     public TResult Invoke<TResult>(Action<IBufferWriter<byte>>? action, Func<IMemoryReadHandle, TResult> func) 
//         => InvokeCore(action, func);
//
//     public void Invoke(Action<IBufferWriter<byte>>? action) 
//         => InvokeCore(action);
//
//     private TResult InvokeCore<TResult>(Action<IBufferWriter<byte>>? action, Func<IMemoryReadHandle, TResult> func,
//         CancellationToken cancellationToken = default)
//     {
//         using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50));
//         using var cts1 = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
//         var token = cts1.Token;
//         
//         var guid = Guid.NewGuid();
//         
//         try
//         {
//             if (action != null)
//             {
//                 using var bufferWriter = new ArrayPoolBufferWriter<byte>();
//                 action(bufferWriter);
//         
//                 SendCore(guid, bufferWriter.WrittenSpan, SignalType.Request, token);
//             }
//             else
//             {
//                 SendCore(guid, Span<byte>.Empty, SignalType.Request, token);
//             }
//         
//             var completionStatus = WaitInvocationExecution(guid, token);
//             if (completionStatus == SignalStatus.Fail)
//             {
//                 throw new InvalidOperationException($"Invocation failed {_code} for \"{Name}\"");
//             }
//         
//             if (func == null) return default;
//         
//             Accessor.Read<InvocationPackage>(_packageOffset, out var result);
//             using var responseReadHandle = GetReadHandle(_packageOffset+_sizeOfPackageHeader, result.PayloadLenght);
//             
//             try
//             {
//                 return func(responseReadHandle);
//             }
//             catch (Exception e)
//             {
//                 _eventsSource.Exception(e);
//                 throw new InvalidOperationException($"failed to deserialize response: {_code}", e);
//             }
//         }
//         finally
//         {
//             CompleteInvocation(guid);
//         }
//     }
//     
//     private void InvokeCore(Action<IBufferWriter<byte>>? action, CancellationToken cancellationToken = default)
//     {
//         using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50));
//         using var cts1 = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
//         var token = cts1.Token;
//         
//         var guid = Guid.NewGuid();
//         
//         try
//         {
//             if (action != null)
//             {
//                 using var bufferWriter = new ArrayPoolBufferWriter<byte>();
//                 action(bufferWriter);
//         
//                 SendCore(guid, bufferWriter.WrittenSpan, SignalType.Invocation, token);
//             }
//             else
//             {
//                 SendCore(guid, Span<byte>.Empty, SignalType.Invocation, token);
//             }
//         
//             var completionStatus = WaitInvocationExecution(guid, token);
//             if (completionStatus == SignalStatus.Fail)
//             {
//                 throw new InvalidOperationException($"Invocation failed {_code} for \"{Name}\"");
//             }
//         }
//         finally
//         {
//             CompleteInvocation(guid);
//         }
//     }
//     
//     private void SendCore(Guid guid, ReadOnlySpan<byte> data, SignalType signalType, CancellationToken cancellationToken = default)
//     {
//         // if (data.Length >= descriptor.LargeSignalSize)
//         // {
//         //     throw new InvalidOperationException("Message data exceed payload buffer");
//         // }
//
//         var spin = new SpinWait();
//         var attempts = 0;
//         while (!cancellationToken.IsCancellationRequested)
//         {
//             cancellationToken.ThrowIfCancellationRequested();
//             
//             lock (_writeSync)
//             {
//                 Accessor.Read<InvocationPackage>(_packageOffset, out var package);
//
//                 if (package.Status is SignalStatus.None)
//                 {
//                     package = new InvocationPackage(guid, _code, SignalStatus.ReadyToProcess, signalType, DateTime.Now.TimeOfDay,
//                         default, data.Length);
//                     if (!data.IsEmpty)
//                     {
//                         var payloadOffset = _packageOffset + _sizeOfPackageHeader;
//                         Accessor.SafeMemoryMappedViewHandle.WriteSpan((ulong)payloadOffset, data);
//                     }
//                     Accessor.Write(_packageOffset, ref package);
//                     return;
//                 }
//
//                 attempts++;
//                 
//                 if (attempts > 100)
//                 {
//                     _eventsSource.Write(() => "send attempts > 100");
//                     attempts = 0;
//                 }
//             }
//
//             spin.SpinOnce();
//         }
//     }
//
//     private SignalStatus WaitInvocationExecution(Guid guid, CancellationToken cancellationToken = default)
//     {
//         var spinWait = new SpinWait();
//         while (true)
//         {
//             cancellationToken.ThrowIfCancellationRequested();
//
//             Accessor.Read<InvocationPackage>(_packageOffset, out var package);
//
//             if (guid == package.Guid)
//             {
//                 if (package.Status is SignalStatus.Processed or SignalStatus.Fail)
//                 {
//                     return package.Status;
//                 }
//             }
//             else
//             {
//                 _eventsSource.Write(() => "guid mismatch");
//             }
//
//             spinWait.SpinOnce();
//         }
//     }
//     
//     private void CompleteInvocation(Guid guid)
//     {
//         var package = new InvocationPackage(guid, _code, SignalStatus.None, SignalType.None, DateTime.Now.TimeOfDay,
//             default, default);
//         Accessor.Write(_packageOffset, ref package);
//     }
//
//     protected override void ReleaseResources()
//     {
//         _taskScheduler.Dispose();
//     }
// }