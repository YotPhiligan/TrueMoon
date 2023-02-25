using TrueMoon.Thorium.IO;
using TrueMoon.Thorium.Signals;
using TrueMoon.Thorium.Utils;

namespace TrueMoon.Thorium;

public class SignalSender : SignalProcessorBase
{
    private readonly Mutex _mutex;
    //private readonly EventWaitHandle _eventWaitHandle;

    public SignalSender(string name) : base(name)
    {
        _mutex = Mutex.OpenExisting($"Global\\{name}_mw");
    }

    public void Request(ReadOnlyMemory<byte> data, Guid code, Action<Signal, IMemoryReadHandle> responsePayloadReadFunc = default,
        CancellationToken cancellationToken = default)
    {
        var signal = SendCore(data.Span, SignalType.Request, code, cancellationToken);

        Signal completion = default;
        try
        {
            completion = WaitMessageCompletion(signal, cancellationToken);

            if (responsePayloadReadFunc == null) return;
            
            var descriptorSize = SignalStorageUtils.DescriptorSize;
            var signalDescriptorSize = SignalStorageUtils.SignalSize;
        
            var descriptor = GetStorageDetails();

            MemoryReadHandle? responseReadHandle = default;
            Signal? responseSignal1 = default;
            if (completion.ResponseLocation == signal.Location)
            {
                var signalSize = completion.Location switch
                {
                    SignalLocation.Small => descriptor.SmallSignalSize,
                    SignalLocation.Large => descriptor.LargeSignalSize,
                    _ => descriptor.SmallSignalSize
                };
        
                var baseOffset = completion.Location switch
                {
                    SignalLocation.Small => descriptorSize,
                    SignalLocation.Large => descriptorSize+descriptor.SmallSignalSize*descriptor.SmallSignals,
                    _ => descriptorSize
                };
            
                var offset = baseOffset + completion.Index * signalSize + signalDescriptorSize;
            
                responseReadHandle = GetReadHandle(offset, completion.ResponseLenght);
            }
            else
            {
                var signalSize = completion.ResponseLocation switch
                {
                    SignalLocation.Small => descriptor.SmallSignalSize,
                    SignalLocation.Large => descriptor.LargeSignalSize,
                    _ => descriptor.SmallSignalSize
                };
        
                var baseOffset = completion.ResponseLocation switch
                {
                    SignalLocation.Small => descriptorSize,
                    SignalLocation.Large => descriptorSize+descriptor.SmallSignalSize*descriptor.SmallSignals,
                    _ => descriptorSize
                };
            
                
                //var offset1 = descriptorSize + descriptor.SmallSignals * descriptor.SmallSignalSize;

                var responseSignalOffset = baseOffset + signalSize * completion.ResponseIndex;
                Accessor.Read<Signal>(responseSignalOffset, out var responseSignal);
                
                if (responseSignal.Status == SignalStatus.ResponsePreparing 
                    && responseSignal.Code == signal.Code)
                {
                    var signalSize1 = responseSignal.Location switch
                    {
                        SignalLocation.Small => descriptor.SmallSignalSize,
                        SignalLocation.Large => descriptor.LargeSignalSize,
                        _ => descriptor.SmallSignalSize
                    };
        
                    var baseOffset1 = responseSignal.Location switch
                    {
                        SignalLocation.Small => descriptorSize,
                        SignalLocation.Large => descriptorSize+descriptor.SmallSignalSize*descriptor.SmallSignals,
                        _ => descriptorSize
                    };
                    var offset = baseOffset1 + responseSignal.Index * signalSize1 + signalDescriptorSize;
            
                    responseReadHandle = GetReadHandle(offset, completion.ResponseLenght);

                    responseSignal1 = responseSignal;
                }
                else
                {
                    throw new InvalidOperationException($"response code do not match with request: {responseSignal.Code} -> {signal.Code}");
                }
                
            }
            
            _mutex.WaitOne();
            try
            {
                responsePayloadReadFunc(completion, responseReadHandle);
            }
            finally
            {
                _mutex.ReleaseMutex();
                responseReadHandle?.Dispose();
                
                if (responseSignal1 != null)
                {
                    try
                    {
                        CompleteSignal(responseSignal1.Value);
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException($"failed to cleanup response signal: {responseSignal1.Value.Code}", e);
                    }
                }
            }
        }
        finally
        {
            CompleteSignal(completion);
        }
    }

    private Signal WaitMessageCompletion(Signal signal, CancellationToken cancellationToken = default)
    {
        var spinWait = new SpinWait();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var descriptorSize = SignalStorageUtils.DescriptorSize;
        
            var descriptor = GetStorageDetails();
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
            
            var offset = baseOffset + signal.Index * signalSize;
            Signal signalOut;
            _mutex.WaitOne();
            try
            {
                Accessor.Read<Signal>(offset, out signalOut);
            }
            finally
            {
                _mutex.ReleaseMutex();
            }

            if (signalOut.Status is SignalStatus.Processed or SignalStatus.Fail)
            {
                return signalOut;
            }

            spinWait.SpinOnce();
        }
    }

    public void Invoke(ReadOnlyMemory<byte> data, Guid code, CancellationToken cancellationToken = default)
    {
        var signal = SendCore(data.Span, SignalType.Invocation, code, cancellationToken);

        Signal signalOut = default;
        try
        {
            signalOut = WaitMessageCompletion(signal, cancellationToken);
        }
        finally
        {
            CompleteSignal(signalOut);
        }
    }

    private void CompleteSignal(Signal inSignal)
    {
        var descriptor = GetStorageDetails();
        
        var descriptorSize = SignalStorageUtils.DescriptorSize;


        var signalSize = inSignal.Location switch
        {
            SignalLocation.Small => descriptor.SmallSignalSize,
            SignalLocation.Large => descriptor.LargeSignalSize,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        var baseOffset = inSignal.Location switch
        {
            SignalLocation.Small => descriptorSize,
            SignalLocation.Large => descriptorSize+descriptor.SmallSignalSize*descriptor.SmallSignals,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        var offset = baseOffset + inSignal.Index * signalSize;
        var signal = new Signal(inSignal.Index, SignalType.None, SignalStatus.None, Guid.Empty, 0, inSignal.Location, inSignal.ResponseLocation, 0, 0);

        _mutex.WaitOne();
        try
        {
            Accessor.Write(offset, ref signal);
        }
        finally
        {
            _mutex.ReleaseMutex();
        }
    }
    
    public void Send(ReadOnlyMemory<byte> data, Guid code, CancellationToken cancellationToken = default)
    {
        SendCore(data.Span, SignalType.Notification, code, cancellationToken);
    }

    private readonly object _sync = new ();
    
    private Signal SendCore(ReadOnlySpan<byte> data, SignalType signalType, Guid code, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var descriptor = GetStorageDetails();
        

        if (data.Length >= descriptor.LargeSignalSize)
        {
            throw new InvalidOperationException("Message data exceed payload buffer");
        }

        var descriptorSize = SignalStorageUtils.DescriptorSize;
        
        var signalSize = descriptor.SmallSignalSize;

        var spin = new SpinWait();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _mutex.WaitOne();
            try
            {
                if (data.Length <= descriptor.SmallSignalSize)
                {
                    for (int i = descriptorSize; i < descriptorSize + descriptor.SmallSignals * signalSize; i += signalSize)
                    {
                        Accessor.Read<Signal>(i, out var signal);

                        if (signal.Status is SignalStatus.None)
                        {
                            var outSignal = new Signal(signal.Index, 
                                signalType, 
                                SignalStatus.ReadyToProcess, 
                                code,
                                data.Length,
                                SignalLocation.Small,
                                SignalLocation.Small,
                                0,
                                0
                            );
                            WriteSignal(descriptorSize, signalSize, signal.Index, outSignal, data);
                            return outSignal;
                        }
                    }
                }
                else
                {
                    signalSize = descriptor.LargeSignalSize;
                    var offset1 = descriptorSize + descriptor.SmallSignals * descriptor.SmallSignalSize;
                    
                    for (int i = offset1; i < offset1 + descriptor.LargeSignals * signalSize; i += signalSize)
                    {
                        Accessor.Read<Signal>(i, out var signal);

                        if (signal.Status is SignalStatus.None)
                        {
                            var outSignal = new Signal(signal.Index, 
                                signalType, 
                                SignalStatus.ReadyToProcess, 
                                code,
                                data.Length,
                                SignalLocation.Large,
                                SignalLocation.Large,
                                0,
                                0
                            );
                            WriteSignal(offset1, signalSize, signal.Index, outSignal, data);
                            return outSignal;
                        }
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

    private void WriteSignal(int baseOffset, int signalSize, int index, Signal signal, ReadOnlySpan<byte> payload)
    {
        try
        {
            var signalDeskSize = SignalStorageUtils.SignalSize;
            var offset = baseOffset + index * signalSize;

            var payloadOffset = offset+signalDeskSize;

            Accessor.SafeMemoryMappedViewHandle.WriteSpan((ulong)payloadOffset, payload);
            Accessor.Write(offset, ref signal);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    protected override void ReleaseResources()
    {
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (Exception)
        {

        }
        
        _mutex.Dispose();
        //_eventWaitHandle.Dispose();
        base.ReleaseResources();
    }
}