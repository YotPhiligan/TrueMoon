namespace TrueMoon.Dependencies;

public static class Extensions
{
    public static IAppBuilder AddSingleton<T, TImplementation>(this IAppBuilder builder) => builder;

    public static IAppBuilder AddSingleton<T>(this IAppBuilder builder, Func<Resolver<T>,T> func)
    {
        
    }
}