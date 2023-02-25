using TrueMoon.Dependencies;

namespace TrueMoon.Extensions.DependencyInjection;

public static class DependencyDescriptorExtensions
{
    public static bool TryGetInstance(this IDependencyDescriptor descriptor, out object? instance)
    {
        instance = descriptor.GetInstance();
        return instance != null;
    }

    public static bool TryGetFactory(this IDependencyDescriptor descriptor, out Func<IServiceProvider, object>? factory)
    {
        factory = GetFactory(descriptor);
        return factory != null;
    }
    
    public static Func<IServiceProvider, object>? GetFactory(this IDependencyDescriptor descriptor)
    {
        var type = descriptor.GetType();
        var prop = type.GetProperty(nameof(IDependencyDescriptor<object>.Factory));
        var value = prop?.GetValue(descriptor);

        Func<IServiceProvider, object>? factory = default;
        
        if (value == null)
        {
            return factory;
        }

        if (value is not Delegate delegate1)
        {
            return factory;
        }

        factory = provider => delegate1.DynamicInvoke(provider)!;
        return factory;
    }
}