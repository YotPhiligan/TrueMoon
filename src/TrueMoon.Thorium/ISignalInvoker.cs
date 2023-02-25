namespace TrueMoon.Thorium;

public interface ISignalInvoker
{
    Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default);
    Task InvokeAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default);
    Task<TResponse?> RequestAsync<TMessage, TResponse>(TMessage message, CancellationToken cancellationToken = default) where TMessage : IWithResponse<TResponse>;
    void Send<TMessage>(TMessage message);
}