namespace TrueMoon;

public interface IStartable
{
    Task StartAsync(CancellationToken cancellationToken = default);
}