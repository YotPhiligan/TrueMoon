namespace TrueMoon.Enclaves;

public interface IEnclaveHandler : IAsyncDisposable, IDisposable
{
    
}

public record EnclaveConfiguration
{
    public bool IsLocal { get; set; }
}

public interface IEnclavesController
{
    
}