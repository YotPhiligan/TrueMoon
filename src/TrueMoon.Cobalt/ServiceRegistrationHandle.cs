namespace TrueMoon.Cobalt;

public class ServiceRegistrationHandle
{
    public Type ServiceType { get; set; }
    public Type? ImplementationType { get; set; }
    public ServiceLifetime Lifetime { get; set; }
    
    public object? Instance { get; set; }
    public Func<IResolver>? Resolver { get; set; }
    
    public string? Key { get; set; }

    public override string ToString() 
        => $"{Lifetime} :{(string.IsNullOrWhiteSpace(Key) ? string.Empty : Key)} {ServiceType} - {ImplementationType ?? Instance ?? Resolver}";
}