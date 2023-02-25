namespace TrueMoon.Thorium;

public interface ISignalHandler<TMessage>
{
    void Process(TMessage message);
}

public interface ISignalHandler<TMessage,TResponse>
{
    TResponse Process(TMessage message);
}

public interface IAsyncSignalHandler<TMessage>
{
    Task ProcessAsync(TMessage message, CancellationToken cancellationToken);
}

public interface IAsyncSignalHandler<TMessage,TResponse>
{
    Task<TResponse> ProcessAsync(TMessage message, CancellationToken cancellationToken);
}