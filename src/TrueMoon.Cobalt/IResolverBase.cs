namespace TrueMoon.Cobalt;

public interface IResolverBase
{
    bool IsServiceDisposable { get; }
    ResolvingServiceLifetime ServiceLifetime { get; }
}