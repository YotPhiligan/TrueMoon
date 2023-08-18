using System.Buffers;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.IO.SharedMemory.Utils;
using TrueMoon.Thorium.IO.Signals;

namespace TrueMoon.Thorium.IO.SharedMemory;

public class MemorySignalClient<T> : MemorySignalProcessorBase, ISignalClient<T>
{
    public MemorySignalClient(IEventsSource<MemorySignalClient<T>> eventsSource, SignalsTaskScheduler signalsTaskScheduler) : base($"tm_{typeof(T).FullName}")
    {
        _eventsSource = eventsSource;
        _taskScheduler = signalsTaskScheduler.TaskScheduler;
    }

    private readonly object _writeSync = new ();

    public Task<TResult> InvokeAsync<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action,
        Func<IMemoryReadHandle, TResult> func,
        CancellationToken cancellationToken = default)
        => Task.Factory.StartNew(() => InvokeCore(methodCode, action, func, cancellationToken), cancellationToken, TaskCreationOptions.PreferFairness, _taskScheduler);
    
    private TResult InvokeCore<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<IMemoryReadHandle, TResult> func,
        CancellationToken cancellationToken = default)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        using var cts1 = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
        var token = cts1.Token;

        Signal signal;
        if (action != null)
        {
            using var bufferWriter = new ArrayPoolBufferWriter<byte>();
            action(bufferWriter);
        
            signal = SendCore(bufferWriter.WrittenSpan, SignalType.Request, methodCode, token);
        }
        else
        {
            signal = SendCore(Span<byte>.Empty, SignalType.Request, methodCode, token);
        }

        Signal completion = default;
        try
        {
            completion = WaitMessageCompletion(signal, token);

            if (completion.Status == SignalStatus.Fail)
            {
                throw new InvalidOperationException($"Failed to invoke signal {methodCode} of service \"{typeof(T)}\"");
            }
            
            if (func == null) return default;
            
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

            Exception? exc = default;
            TResult output = default;
            try
            {
                output = func(responseReadHandle);
            }
            catch (Exception e)
            {
                _eventsSource.Exception(e);
            }
            finally
            {
                responseReadHandle?.Dispose();
                
                if (responseSignal1 != null)
                {
                    try
                    {
                        CompleteSignal(responseSignal1.Value);
                    }
                    catch (Exception e)
                    {
                        exc = e;
                    }
                }
            }

            if (exc != null)
            {
                throw new InvalidOperationException($"failed to cleanup response signal: {responseSignal1.Value.Code}", exc);
            }

            return output;
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
            
            Accessor.Read<Signal>(offset, out signalOut);
            
            if (signalOut.Status is SignalStatus.Processed or SignalStatus.Fail)
            {
                return signalOut;
            }

            spinWait.SpinOnce();
        }
    }

    public Task InvokeAsync(byte methodCode, Action<IBufferWriter<byte>>? action,
        CancellationToken cancellationToken = default)
        => Task.Factory.StartNew(() => InvokeCore(methodCode, action, cancellationToken), cancellationToken, TaskCreationOptions.PreferFairness, _taskScheduler);

    public TResult Invoke<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<IMemoryReadHandle, TResult> func) 
        => InvokeCore(methodCode, action, func);

    public void Invoke(byte methodCode, Action<IBufferWriter<byte>>? action) => InvokeCore(methodCode, action);

    private void InvokeCore(byte methodCode, Action<IBufferWriter<byte>>? action, CancellationToken cancellationToken = default)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        using var cts1 = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
        var token = cts1.Token;

        Signal signal;
        if (action != null)
        {
            using var bufferWriter = new ArrayPoolBufferWriter<byte>();
            action(bufferWriter);
        
            signal = SendCore(bufferWriter.WrittenSpan, SignalType.Invocation, methodCode, token);
        }
        else
        {
            signal = SendCore(Span<byte>.Empty, SignalType.Invocation, methodCode, token);
        }

        Signal signalOut = default;
        try
        {
            signalOut = WaitMessageCompletion(signal, token);
        }
        finally
        {
            CompleteSignal(signalOut);
        }
        
        if (signalOut.Status == SignalStatus.Fail)
        {
            throw new InvalidOperationException($"Failed to invoke signal {methodCode} of service \"{typeof(T)}\"");
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
        var signal = new Signal(inSignal.Index, SignalType.None, SignalStatus.None, 0, Guid.Empty, 0, inSignal.Location, inSignal.ResponseLocation, 0, 0);

        lock (_writeSync)
        {
            Accessor.Write(offset, ref signal);
        }
    }

    private readonly TaskScheduler _taskScheduler;
    private readonly IEventsSource<MemorySignalClient<T>> _eventsSource;

    private Signal SendCore(ReadOnlySpan<byte> data, SignalType signalType, byte methodCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var descriptor = GetStorageDetails();

        var guid = Guid.NewGuid();

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
            
            lock (_writeSync)
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
                                methodCode, 
                                guid,
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
                                methodCode, 
                                guid,
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
}