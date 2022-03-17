namespace TrueMoon.Dependencies;

public record Descriptor<T> : IDescriptor
{
    public Type Type { get; set; }
    public Type? Implementation { get; set; }
    public Func<IServiceProvider,T>? Factory { get; set; }
}