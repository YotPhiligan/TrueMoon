namespace TrueMoon.Dependencies;

public static class ServiceProviderExtensions
{
    public static T? Resolve<T>(this IServiceProvider serviceProvider) 
        => serviceProvider.GetService(typeof(T)) is T value 
            ? value 
            : default;
}