using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MemoryPack;
using MemoryPack.Internal;
using TrueMoon.Thorium.IO;

namespace TrueMoon.Thorium;

public class SignalHandleWrapper<TMessage> : ISignalHandleWrapper<TMessage>
{
    private readonly IReadOnlyList<ISignalHandler<TMessage>> _messageHandlers;
    private readonly IAsyncSignalHandler<TMessage>[] _asyncMessageHandlers;
    
    private readonly ISignalHandler<TMessage>? _messageHandlerSingle;
    private readonly IAsyncSignalHandler<TMessage>? _asyncMessageHandlerSingle;

    public SignalHandleWrapper(IEnumerable<ISignalHandler<TMessage>> messageHandlers, IEnumerable<IAsyncSignalHandler<TMessage>> asyncMessageHandlers)
    {
        _messageHandlers = messageHandlers.ToList();
        if (_messageHandlers.Count == 1)
        {
            _messageHandlerSingle = _messageHandlers[0];
        }
        _asyncMessageHandlers = asyncMessageHandlers.ToArray();
        
        if (_asyncMessageHandlers.Length == 1)
        {
            _asyncMessageHandlerSingle = _asyncMessageHandlers[0];
        }
    }
    
    public void ProcessMessage(Guid code, IMemoryReadHandle readHandle, IMemoryWriteHandle? writeHandle,
        CancellationToken cancellationToken)
    {
        TMessage? message = default;

        try
        {
            var readOnlySpan = readHandle.GetData();
            MemoryPackSerializer.Deserialize<TMessage>(readOnlySpan, ref message);
            
            if (_messageHandlerSingle != null)
            {
                _messageHandlerSingle.Process(message);
            }
            else
            {
                foreach (var handler in _messageHandlers)
                {
                    handler.Process(message);
                }
            }
            
            if (_asyncMessageHandlerSingle != null)
            {
                Task.Factory
                    .StartNew(()=>_asyncMessageHandlerSingle.ProcessAsync(message, cancellationToken), cancellationToken, TaskCreationOptions.PreferFairness, TaskScheduler.Default)
                    .Unwrap()
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                Task.Factory
                    .StartNew(()=>Task.WhenAll(_asyncMessageHandlers.Select(t=>t.ProcessAsync(message, cancellationToken))), cancellationToken, TaskCreationOptions.PreferFairness, TaskScheduler.Default)
                    .Unwrap()
                    .GetAwaiter()
                    .GetResult();
            }
        }
        finally
        {
            if (message is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

public class SignalHandleWrapper<TMessage,TResponse> : ISignalHandleWrapper<TMessage,TResponse>
{
    private readonly ISignalHandler<TMessage, TResponse>? _messageHandler;
    private readonly IAsyncSignalHandler<TMessage, TResponse>? _asyncMessageHandler;

    public SignalHandleWrapper(ISignalHandler<TMessage,TResponse>? messageHandler = default, IAsyncSignalHandler<TMessage,TResponse>? asyncMessageHandler = default)
    {
        _messageHandler = messageHandler;
        _asyncMessageHandler = asyncMessageHandler;
    }

    public void ProcessMessage(Guid code, IMemoryReadHandle readHandle, IMemoryWriteHandle? writeHandle,
        CancellationToken cancellationToken)
    {
        TMessage? message = default;
        try
        {
            var readOnlySpan = readHandle.GetData();
            message = MemoryPackSerializer.Deserialize<TMessage>(readOnlySpan);
            //message = JsonSerializer.Deserialize<TMessage>(readOnlySpan);

            TResponse response;
            if (_asyncMessageHandler != null)
            {
                response = _asyncMessageHandler.ProcessAsync(message, cancellationToken).GetAwaiter().GetResult();
            }
            else if (_messageHandler != null)
            {
                response = _messageHandler.Process(message);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Handler not found for \"{typeof(TMessage)}, {typeof(TResponse)}\" message");
            }

            using var bufferWriter = new ArrayPoolBufferWriter<byte>();
            //using var writer = new Utf8JsonWriter(bufferWriter);
            MemoryPackSerializer.Serialize(bufferWriter,response);
            //JsonSerializer.Serialize(writer, response);

            writeHandle?.Write(bufferWriter.WrittenMemory, cancellationToken);
        }
        // catch (Exception e)
        // {
        //     //var b = readHandle.GetData().ToArray();
        //     //var str = Encoding.Default.GetString(b);
        // }
        finally
        {
            if (message is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}