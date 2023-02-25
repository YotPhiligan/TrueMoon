using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.IO;
using TrueMoon.Thorium.Signals;
using TrueMoon.Thorium.Utils;
using TrueMoon.Threading;

namespace TrueMoon.Thorium;

public class SignalListener : SignalProcessorBase
{
    private readonly IEventsSource _eventsSource;
    private readonly Guid[] _listenCodes;
    private readonly int _readThreads;
    private TmTaskScheduler _taskScheduler;
    private TmTaskScheduler _messageProcessingTaskScheduler;
    
    private Action<Signal, IMemoryReadHandle, IMemoryWriteHandle?, CancellationToken> _onMessageFunc;
    
    private readonly Mutex _mutex;
    private CancellationTokenSource _cts;
    private bool _isInitialized;

    public SignalListener(string name, IEventsSource eventsSource, Guid[] listenCodes, int readThreads = 4) : base(name)
    {
        _eventsSource = eventsSource;
        _listenCodes = listenCodes;
        _readThreads = readThreads;

        _mutex = Mutex.OpenExisting($"Global\\{name}_mw");
    }

    public void Listen(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _taskScheduler = new TmTaskScheduler(nameof(SignalListener), 1,_cts.Token);
        _messageProcessingTaskScheduler = new TmTaskScheduler(nameof(SignalListener), _readThreads, _cts.Token);
        Task.Factory.StartNew(() => ListenCore(_cts.Token), _cts.Token, TaskCreationOptions.PreferFairness,
            _taskScheduler);

        _isInitialized = true;
    }
    
    private void ListenCore(CancellationToken cancellationToken = default)
    {
        var descriptor = GetStorageDetails();
        
        var descriptorSize = SignalStorageUtils.DescriptorSize;
        int offset = descriptorSize;
        var spin = new SpinWait();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            offset = descriptorSize;
            var signalSize = descriptor.SmallSignalSize;
            
            _mutex.WaitOne();
            try
            {
                for (int i = offset; i < descriptorSize + descriptor.SmallSignals * signalSize; i += signalSize)
                {
                    Accessor.Read<Signal>(i, out var signal);

                    if (signal.Status is SignalStatus.ReadyToProcess && _listenCodes.Contains(signal.Code))
                    {
                        var outSignal = new Signal(signal.Index,
                            signal.Type,
                            SignalStatus.Processing,
                            signal.Code,
                            signal.PayloadLenght,
                            SignalLocation.Small,
                            SignalLocation.Small,
                            0,
                            0
                        );

                        Accessor.Write(i, ref outSignal);

                        ProcessSignalItem(signal, cancellationToken);
                    }
                }
                
                offset += descriptor.SmallSignals * descriptor.SmallSignalSize;
            
                signalSize = descriptor.LargeSignalSize;
            
                for (int i = offset; i < offset + descriptor.LargeSignals * signalSize; i += signalSize)
                {
                    Accessor.Read<Signal>(i, out var signal);

                    if (signal.Status is SignalStatus.ReadyToProcess && _listenCodes.Contains(signal.Code))
                    {
                        var outSignal = new Signal(signal.Index,
                            signal.Type,
                            SignalStatus.Processing,
                            signal.Code,
                            signal.PayloadLenght,
                            SignalLocation.Large,
                            SignalLocation.Large,
                            0,
                            0
                        );
                        
                        Accessor.Write(i, ref outSignal);

                        ProcessSignalItem(signal, cancellationToken);
                    }
                }
            }
            finally
            {
                _mutex.ReleaseMutex();
            }

            spin.SpinOnce();
        }
    }

    private void ProcessSignalItem(Signal signal, CancellationToken cancellationToken)
    {
        Task.Factory.StartNew(() => ProcessSignal(signal, cancellationToken),
            cancellationToken, TaskCreationOptions.PreferFairness,
            _messageProcessingTaskScheduler);
    }

    private void ProcessSignal(Signal signal, CancellationToken cancellationToken = default)
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
            if (_onMessageFunc == null) return;
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

            var writeHandle = signal.Type switch
            {
                SignalType.Request => new MemoryWriteHandle(signal, Accessor, _mutex),
                //SignalType.Request => new MemoryWriteHandle(message, Accessor, _eventWaitHandle),
                _ => default
            };
                
            _onMessageFunc.Invoke(signal, readHandle, writeHandle, cancellationToken);

            responseDetails = writeHandle?.GetResponseDetails();
        }
        catch(Exception e)
        {
            if (signal.Type is SignalType.Invocation or SignalType.Request)
            {
                resultCode = SignalStatus.Fail;
            }

            _eventsSource.Write(() => e, $"{nameof(SignalListener)}.Exception");
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
                ? new Signal(signal.Index, signal.Type, resultCode, Guid.Empty, 0, signal.Location, responseDetails.Value.location, responseDetails.Value.index, responseDetails.Value.size)
                : new Signal(signal.Index, signal.Type, resultCode, Guid.Empty, 0, signal.Location, signal.ResponseLocation, 0, 0);

            _mutex.WaitOne();
            try
            {
                Accessor.Write(offset, ref outSignal);
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }
    }

    public void OnSignal(Action<Signal, IMemoryReadHandle, IMemoryWriteHandle?, CancellationToken> func)
    {
        _onMessageFunc = func;
    }
    
    protected override void ReleaseResources()
    {
        _cts.Cancel();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (Exception)
        {
            
        }
        _mutex.Dispose();
        //_eventWaitHandle.Dispose();
        _taskScheduler.Dispose();
        _messageProcessingTaskScheduler.Dispose();
        _cts.Dispose();
    }
    
    private class MemoryWriteHandle : IMemoryWriteHandle
    {
        private readonly Signal _signal;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly Mutex _mutex;

        private SignalLocation _responseLocation;

        private byte _responseIndex;

        private int _responseLenght;
        //private readonly EventWaitHandle _eventWaitHandle;

        public MemoryWriteHandle(Signal signal, MemoryMappedViewAccessor accessor, Mutex mutex)
        {
            _signal = signal;
            _accessor = accessor;
            _mutex = mutex;
            //_eventWaitHandle = eventWaitHandle;
        }

        public void Write(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            var responseLenght = data.Length;
                
            var descriptor = SignalStorageUtils.GetDescriptor(_accessor);
        
            var descriptorSize = SignalStorageUtils.DescriptorSize;
            var signalDeskSize = SignalStorageUtils.SignalSize;


            var signalSize = _signal.Location switch
            {
                SignalLocation.Small => descriptor.SmallSignalSize,
                SignalLocation.Large => descriptor.LargeSignalSize,
                _ => throw new ArgumentOutOfRangeException()
            };

            var baseOffset = _signal.Location switch
            {
                SignalLocation.Small => descriptorSize,
                SignalLocation.Large => descriptorSize+descriptor.SmallSignalSize*descriptor.SmallSignals,
                _ => throw new ArgumentOutOfRangeException()
            };
        
            var offset = baseOffset + _signal.Index * signalSize + signalDeskSize;
                
            if (_signal.Location == SignalLocation.Small && responseLenght > descriptor.SmallSignalSize - signalDeskSize)
            {
                WriteLargeSignalResponse(data, cancellationToken);
            }
            else
            {
                _mutex.WaitOne();
                try
                {
                    _accessor.SafeMemoryMappedViewHandle.WriteSpan((ulong)offset,data.Span);
                }
                finally
                {
                    _mutex.ReleaseMutex();
                }

                _responseLocation = _signal.Location;
                _responseIndex = _signal.Index;
                _responseLenght = responseLenght;
            }
        }
        
        private void WriteLargeSignalResponse(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {

            var descriptor = SignalStorageUtils.GetDescriptor(_accessor);

            var descriptorSize = SignalStorageUtils.DescriptorSize;
            var signalDeskSize = SignalStorageUtils.SignalSize;
        
            var signalSize = descriptor.LargeSignalSize;

            var offset = descriptorSize + descriptor.SmallSignals * descriptor.SmallSignalSize;
            
            var spin = new SpinWait();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _mutex.WaitOne();
                try
                {
                    for (int i = 0; i < descriptor.LargeSignals; i++)
                    {
                        var signalOffset = offset + signalSize * i;
                        _accessor.Read<Signal>(signalOffset, out var signal);

                        if (signal.Status is SignalStatus.None)
                        {
                            var outSignal = new Signal(signal.Index, 
                                _signal.Type, 
                                SignalStatus.ResponsePreparing,
                                _signal.Code,
                                data.Length,
                                SignalLocation.Large,
                                SignalLocation.Large,
                                0,
                                0
                            );
                        
                            _accessor.SafeMemoryMappedViewHandle.WriteSpan((ulong)(signalOffset+signalDeskSize), data.Span);
                            _accessor.Write(signalOffset, ref outSignal);

                        
                            _responseLocation = outSignal.Location;
                            _responseIndex = outSignal.Index;
                            _responseLenght = data.Length;
                            return;
                        }
                    }
                }
                finally
                {
                    _mutex.ReleaseMutex();
                }

                spin.SpinOnce();
            }
        }

        public (SignalLocation location, byte index, int size) GetResponseDetails() 
            => (_responseLocation, _responseIndex, _responseLenght);
    }
}