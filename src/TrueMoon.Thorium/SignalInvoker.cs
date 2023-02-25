using System.Buffers;
using System.Text;
using System.Text.Json;
using MemoryPack;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.IO;
using TrueMoon.Thorium.Signals;
using TrueMoon.Threading;

namespace TrueMoon.Thorium;

public class SignalInvoker : ISignalInvoker, IDisposable
{
    private readonly ThoriumConfiguration _thoriumConfiguration;
    private readonly SignalMappingStorage _signalMappingStorage;
    private readonly IEventsSource<SignalInvoker>? _eventsSource;
    private readonly SignalSender _sender;
    private readonly TmTaskScheduler _signalProcessingTaskScheduler;

    public SignalInvoker(ThoriumConfiguration thoriumConfiguration, SignalMappingStorage signalMappingStorage, IEventsSource<SignalInvoker>? eventsSource = default)
    {
        _thoriumConfiguration = thoriumConfiguration;
        _signalMappingStorage = signalMappingStorage;
        _eventsSource = eventsSource;
        _sender = new SignalSender(_thoriumConfiguration.Name);
        _signalProcessingTaskScheduler = new TmTaskScheduler(nameof(SignalInvoker), _thoriumConfiguration.WriteThreads);
        //_signalProcessingTaskScheduler = new TmTaskScheduler(nameof(SignalInvoker), 1);
    }

    /// <inheritdoc />
    public Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) =>
        Task.Factory.StartNew(() =>
            {
                var code = _signalMappingStorage.GetCode<TMessage>();

                using var bufferWriter = new ArrayPoolBufferWriter<byte>();
                MemoryPackSerializer.Serialize(bufferWriter,message);
                
                _sender.Send(bufferWriter.WrittenMemory, code, cancellationToken);
            }, cancellationToken, TaskCreationOptions.PreferFairness,
            _signalProcessingTaskScheduler);

    /// <inheritdoc />
    public Task InvokeAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) =>
        Task.Factory.StartNew(() =>
            {
                var code = _signalMappingStorage.GetCode<TMessage>();

                using var bufferWriter = new ArrayPoolBufferWriter<byte>();
                MemoryPackSerializer.Serialize(bufferWriter,message);
                
                _sender.Invoke(bufferWriter.WrittenMemory, code, cancellationToken);
            }, cancellationToken, TaskCreationOptions.PreferFairness,
            _signalProcessingTaskScheduler);

    public Task<TResponse?> RequestAsync<TMessage,TResponse>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : IWithResponse<TResponse> =>
        Task.Factory.StartNew(() =>
            {
                var code = _signalMappingStorage.GetCode<TMessage>();

                using var bufferWriter = new ArrayPoolBufferWriter<byte>();
                MemoryPackSerializer.Serialize(bufferWriter,message);
                //using var writer = new Utf8JsonWriter(bufferWriter);
                //JsonSerializer.Serialize(writer, message);

                TResponse? response = default;
                
                _sender.Request(bufferWriter.WrittenMemory, code, (status, handle) =>
                {
                    if (status.Status != SignalStatus.Processed) return;

                    response = ProcessResponse<TResponse>(handle);
                }, cancellationToken);
                
                return response;
            }, cancellationToken, TaskCreationOptions.PreferFairness,
            _signalProcessingTaskScheduler);

    private TResponse? ProcessResponse<TResponse>(IMemoryReadHandle handle)
    {
        TResponse? response = default;
        try
        {
            var span = handle.GetData();
            response = MemoryPackSerializer.Deserialize<TResponse>(span);
            //response = JsonSerializer.Deserialize<TResponse>(span);
            
            //var m = handle.GetData().ToArray();

            //var str = Encoding.Default.GetString(m);
        }
        catch (Exception e)
        {
            _eventsSource?.Exception(e);
            //var b = handle.GetData().ToArray();

            //var str = Encoding.Default.GetString(b);
            //MemoryPackSerializer.Deserialize<TResponse>(t, ref response);
        }

        return response;
    }

    public void Send<TMessage>(TMessage message)
    {
        var code = _signalMappingStorage.GetCode<TMessage>();

        using var bufferWriter = new ArrayPoolBufferWriter<byte>();
        MemoryPackSerializer.Serialize(bufferWriter,message);
        _sender.Send(bufferWriter.WrittenMemory, code);
    }
    
    public void Send(ReadOnlyMemory<byte> data, Guid code, Action? cleanAction = default)
    {
        try
        {
            _sender.Send(data, code);
        }
        finally
        {
            cleanAction?.Invoke();
        }
    }

    public void Dispose()
    {
        _signalProcessingTaskScheduler.Dispose();
        _sender.Dispose();
    }
}