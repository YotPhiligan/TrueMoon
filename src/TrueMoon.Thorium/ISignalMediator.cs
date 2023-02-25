namespace TrueMoon.Thorium;

public interface ISignalMediator
{
    void Notify<TMessage>(TMessage message);
    TResponse Request<TMessage,TResponse>(TMessage message);
    
    Task NotifyAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default);
    Task<TResponse> RequestAsync<TMessage,TResponse>(TMessage message, CancellationToken cancellationToken = default);
}