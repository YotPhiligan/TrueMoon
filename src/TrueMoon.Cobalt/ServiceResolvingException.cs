namespace TrueMoon.Cobalt;

public class ServiceResolvingException : Exception
{
    public ServiceResolvingException(Type type) : base($"Could not resolve service for type: {type}")
    {
        
    }
}

public class ServiceResolvingException<T> : ServiceResolvingException
{
    public ServiceResolvingException() : base(typeof(T))
    {
    }
}