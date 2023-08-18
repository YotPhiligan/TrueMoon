using TrueMoon.Diagnostics;
using TrueMoon.Thorium.IO.SharedMemory.Utils;
using TrueMoon.Thorium.IO.Signals;
using TrueMoon.Threading;

namespace TrueMoon.Thorium.IO.SharedMemory;

public class MemorySignalServer<T> : MemorySignalProcessorBase, ISignalServer<T>
{
    protected readonly IEventsSource EventsSource;
    private readonly ISignalServerHandler<T> _handler;
    private readonly int _readThreads;
    private readonly TaskScheduler _taskScheduler;

    private CancellationTokenSource _cts;
    private bool _isInitialized;

    public MemorySignalServer(IEventsSource<MemorySignalServer<T>> eventsSource, SignalsTaskScheduler taskScheduler, ISignalServerHandler<T> handler) : base($"tm_{typeof(T).FullName}")
    {
        _taskScheduler = taskScheduler.TaskScheduler;
        EventsSource = eventsSource;
        _handler = handler;
        _readThreads = 4;
        Listen();
    }

    private void Listen()
    {
        if (_isInitialized)
        {
            return;
        }
        _cts = new CancellationTokenSource();
        Task.Factory.StartNew(() =>
            {
                try
                {
                    ListenCore(_cts.Token);
                }
                catch (OperationCanceledException) {}
                catch (Exception e)
                {
                    EventsSource.Exception(e);
                }
            }, _cts.Token, TaskCreationOptions.PreferFairness,
            _taskScheduler);

        _isInitialized = true;
    }

    private void ListenCore(CancellationToken cancellationToken = default)
    {
        var descriptor = GetStorageDetails();
        
        var descriptorSize = SignalStorageUtils.DescriptorSize;
        int offset = descriptorSize;
        var spin = new SpinWait();
        while (!cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();

            offset = descriptorSize;
            var signalSize = descriptor.SmallSignalSize;
            
            for (int i = offset; i < descriptorSize + descriptor.SmallSignals * signalSize; i += signalSize)
            {
                Accessor.Read<Signal>(i, out var signal);

                if (signal.Status is SignalStatus.ReadyToProcess)
                {
                    var outSignal = signal with
                    {
                        Status = SignalStatus.Processing,
                        Location = SignalLocation.Small,
                        ResponseLocation = SignalLocation.Small,
                        ResponseIndex = 0,
                        ResponseLenght = 0
                    };

                    Accessor.Write(i, ref outSignal);

                    ProcessSignalItem(signal, cancellationToken);
                }
            }
            
            offset += descriptor.SmallSignals * descriptor.SmallSignalSize;
        
            signalSize = descriptor.LargeSignalSize;
        
            for (int i = offset; i < offset + descriptor.LargeSignals * signalSize; i += signalSize)
            {
                Accessor.Read<Signal>(i, out var signal);

                if (signal.Status is SignalStatus.ReadyToProcess)
                {
                    var outSignal = signal with
                    {
                        Status = SignalStatus.Processing,
                        Location = SignalLocation.Large,
                        ResponseLocation = SignalLocation.Large,
                        ResponseIndex = 0,
                        ResponseLenght = 0
                    };
                    
                    Accessor.Write(i, ref outSignal);

                    ProcessSignalItem(signal, cancellationToken);
                }
            }

            spin.SpinOnce();
        }
    }

    private void ProcessSignalItem(Signal signal, CancellationToken cancellationToken)
    {
        Task.Factory.StartNew(async () => await ProcessSignalAsync(signal, cancellationToken),
            cancellationToken, TaskCreationOptions.PreferFairness,
            _taskScheduler);
    }

    private async Task ProcessSignalAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        var resultCode = signal.Type switch
        {
            SignalType.None => SignalStatus.None,
            SignalType.Notification => SignalStatus.None,
            SignalType.Invocation => SignalStatus.Processed,
            SignalType.Request => SignalStatus.Processed,
            _ => SignalStatus.Processed
        };
        (SignalLocation location, byte index, int size)? responseDetails = default;
        try
        {
            var descriptorSize = SignalStorageUtils.DescriptorSize;
            var signalDescriptorSize = SignalStorageUtils.SignalSize;
        
            var descriptor = GetStorageDetails();
            var signalSize = signal.Location switch
            {
                SignalLocation.Small => descriptor.SmallSignalSize,
                SignalLocation.Large => descriptor.LargeSignalSize,
                _ => descriptor.SmallSignalSize
            };
        
            var baseOffset = signal.Location switch
            {
                SignalLocation.Small => descriptorSize,
                SignalLocation.Large => descriptorSize+descriptor.SmallSignalSize*descriptor.SmallSignals,
                _ => descriptorSize
            };
            
            var offset = baseOffset + signal.Index * signalSize + signalDescriptorSize;
            
            using var readHandle = GetReadHandle( offset, signal.PayloadLenght);

            using var writer = new ArrayPoolBufferWriter<byte>(128);
                
            var processResult = await _handler.HandleAsync(signal.Code, readHandle, writer, cancellationToken);

            if (processResult)
            {
                responseDetails = WriteResponseCore(signal, writer.WrittenMemory, cancellationToken);
            }
            else
            {
                resultCode = SignalStatus.Fail;
            }
        }
        catch(Exception e)
        {
            if (signal.Type is SignalType.Invocation or SignalType.Request)
            {
                resultCode = SignalStatus.Fail;
            }

            EventsSource.Exception(e);
        }
        finally
        {
            var descriptor = GetStorageDetails();
        
            var descriptorSize = SignalStorageUtils.DescriptorSize;


            var signalSize = signal.Location switch
            {
                SignalLocation.Small => descriptor.SmallSignalSize,
                SignalLocation.Large => descriptor.LargeSignalSize,
                _ => descriptor.SmallSignalSize
            };
        
            var baseOffset = signal.Location switch
            {
                SignalLocation.Small => descriptorSize,
                SignalLocation.Large => descriptorSize+descriptor.SmallSignalSize*descriptor.SmallSignals,
                _ => descriptorSize
            };
        
            var offset = baseOffset + signal.Index * signalSize;
            var outSignal = responseDetails != null
                ? new Signal(signal.Index, signal.Type, resultCode, 0, Guid.Empty, 0, signal.Location, responseDetails.Value.location, responseDetails.Value.index, responseDetails.Value.size)
                : new Signal(signal.Index, signal.Type, resultCode, 0, Guid.Empty, 0, signal.Location, signal.ResponseLocation, 0, 0);

            Accessor.Write(offset, ref outSignal);
        }
    }

    protected override void ReleaseResources()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
    
    private (SignalLocation location, byte index, int size)? WriteResponseCore(Signal signal, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var responseLenght = data.Length;
            
        var descriptor = SignalStorageUtils.GetDescriptor(Accessor);
    
        var descriptorSize = SignalStorageUtils.DescriptorSize;
        var signalDeskSize = SignalStorageUtils.SignalSize;


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
    
        var offset = baseOffset + signal.Index * signalSize + signalDeskSize;
            
        if (signal.Location == SignalLocation.Small && responseLenght > descriptor.SmallSignalSize - signalDeskSize)
        {
            return WriteLargeSignalResponseCore(data, cancellationToken);
        }
        Accessor.SafeMemoryMappedViewHandle.WriteSpan((ulong)offset,data.Span);

        return (signal.Location, signal.Index, responseLenght);
    }
    
    private (SignalLocation location, byte index, int size)? WriteLargeSignalResponseCore(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {

        var descriptor = SignalStorageUtils.GetDescriptor(Accessor);

        var descriptorSize = SignalStorageUtils.DescriptorSize;
        var signalDeskSize = SignalStorageUtils.SignalSize;
    
        var signalSize = descriptor.LargeSignalSize;

        var offset = descriptorSize + descriptor.SmallSignals * descriptor.SmallSignalSize;
        
        var spin = new SpinWait();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int i = 0; i < descriptor.LargeSignals; i++)
            {
                var signalOffset = offset + signalSize * i;
                Accessor.Read<Signal>(signalOffset, out var signal);

                if (signal.Status is SignalStatus.None)
                {
                    var outSignal = new Signal(signal.Index, 
                        signal.Type, 
                        SignalStatus.ResponsePreparing,
                        signal.Code,
                        signal.Guid,
                        data.Length,
                        SignalLocation.Large,
                        SignalLocation.Large,
                        0,
                        0
                    );
                    
                    Accessor.SafeMemoryMappedViewHandle.WriteSpan((ulong)(signalOffset+signalDeskSize), data.Span);
                    Accessor.Write(signalOffset, ref outSignal);
                    
                    return (outSignal.Location,outSignal.Index,data.Length);
                }
            }

            spin.SpinOnce();
        }
    }

    public string Id => typeof(T).FullName!;
}