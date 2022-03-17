namespace TrueMoon;

public interface IStoppable
{
    Task StopAsync(CancellationToken cancellationToken = default);
}