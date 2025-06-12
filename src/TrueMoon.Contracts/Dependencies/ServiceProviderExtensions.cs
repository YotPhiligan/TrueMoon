using System.Collections;

namespace TrueMoon.Dependencies;

public static class ServiceProviderExtensions
{
    public static T? Resolve<T>(this IServiceProvider serviceProvider) 
        => serviceProvider.GetService(typeof(T)) is T value 
            ? value 
            : default;
    
    public static IEnumerable<T> ResolveAll<T>(this IServiceProvider serviceProvider)
    {
        var o = serviceProvider.GetService(typeof(IEnumerable<T>));
        if (o is IEnumerable v)
        {
            var r = v.Cast<T>();

            return r;
        }
        return o as IEnumerable<T> ?? [];
    }
}