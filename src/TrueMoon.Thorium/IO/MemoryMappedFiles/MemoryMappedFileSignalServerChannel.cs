// using System.Runtime.CompilerServices;
// using TrueMoon.Diagnostics;
// using TrueMoon.Thorium.IO.Signals;
// using TrueMoon.Threading;
//
// namespace TrueMoon.Thorium.IO.MemoryMappedFiles;
//
// public class MemoryMappedFileSignalServerChannel : MemoryMappedFileInvocationProcessorBase
// {
//     private readonly IEventsSource _eventsSource;
//     private readonly IInvocationServerHandler _handler;
//     private readonly ThoriumConfiguration _configuration;
//     private readonly TaskScheduler _processTaskScheduler;
//
//     private CancellationTokenSource _cts;
//     private bool _isInitialized;
//     private readonly TmTaskScheduler _listenTaskScheduler;
//     private readonly byte _code;
//     private int _packageOffset;
//     private readonly int _sizeOfPackageHeader = Unsafe.SizeOf<InvocationPackage>();
//
//     public MemoryMappedFileSignalServerChannel(string name, byte code, IEventsSource eventsSource, IInvocationServerHandler handler, ThoriumConfiguration configuration) : base(name)
//     {
//         _code = code;
//         _processTaskScheduler = new TmTaskScheduler($"{name}_{code}_process", 2);
//         _listenTaskScheduler = new TmTaskScheduler($"{name}_{code}_listen", 1);
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
//         _packageOffset = (Unsafe.SizeOf<InvocationPackage>() + _configuration.LargeSignalSize) * _code;
//         _cts = new CancellationTokenSource();
//         Task.Factory.StartNew(() =>
//             {
//                 try
//                 {
//                     ListenCore(_cts.Token);
//                 }
//                 catch (OperationCanceledException) {}
//                 catch (Exception e)
//                 {
//                     _eventsSource.Exception(e);
//                 }
//             }, _cts.Token, TaskCreationOptions.LongRunning,
//             _listenTaskScheduler);
//
//         _isInitialized = true;
//     }
//
//     private void ListenCore(CancellationToken cancellationToken = default)
//     {
//         var spin = new SpinWait();
//         while (!cancellationToken.IsCancellationRequested)
//         {
//             cancellationToken.ThrowIfCancellationRequested();
//
//             Accessor.Read<InvocationPackage>(_packageOffset, out var package);
//
//             if (package.Code == _code && package.Status is SignalStatus.ReadyToProcess)
//             {
//                 var outPackage = package with
//                 {
//                     Status = SignalStatus.Processing,
//                 };
//
//                 Accessor.Write(_packageOffset, ref outPackage);
//
//                 ProcessSignalItem(package, cancellationToken);
//             }
//
//             spin.SpinOnce();
//         }
//     }
//
//     private void ProcessSignalItem(InvocationPackage package, CancellationToken cancellationToken)
//     {
//         Task.Factory.StartNew(async () => await ProcessSignalAsync(package, cancellationToken),
//             cancellationToken, TaskCreationOptions.PreferFairness,
//             _processTaskScheduler);
//     }
//
//     private async Task ProcessSignalAsync(InvocationPackage invocationPackage, CancellationToken cancellationToken = default)
//     {
//         var resultCode = SignalStatus.Processed;
//         var payloadLenght = 0;
//         try
//         {
//             var offset = _packageOffset + _sizeOfPackageHeader;
//             
//             using var readHandle = GetReadHandle(offset, invocationPackage.PayloadLenght);
//
//             using var writer = new ArrayPoolBufferWriter<byte>(128);
//                 
//             var processResult = await _handler.HandleAsync(_code, readHandle, writer, cancellationToken);
//
//             if (processResult)
//             {
//                 payloadLenght = writer.WrittenCount;
//                 Accessor.SafeMemoryMappedViewHandle.WriteSpan((ulong)offset, writer.WrittenSpan);
//             }
//             else
//             {
//                 resultCode = SignalStatus.Fail;
//             }
//         }
//         catch(Exception e)
//         {
//             if (invocationPackage.Type is SignalType.Invocation or SignalType.Request)
//             {
//                 resultCode = SignalStatus.Fail;
//             }
//
//             _eventsSource.Exception(e);
//         }
//         finally
//         {
//             var package = invocationPackage with
//             {
//                 Status = resultCode,
//                 CompletionTime = DateTime.Now.TimeOfDay,
//                 PayloadLenght = payloadLenght
//             };
//             Accessor.Write(_packageOffset, ref package);
//         }
//     }
//
//     protected override void ReleaseResources()
//     {
//         _cts.Cancel();
//         _cts.Dispose();
//         _listenTaskScheduler.Dispose();
//     }
// }