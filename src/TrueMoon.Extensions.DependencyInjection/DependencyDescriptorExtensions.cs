using TrueMoon.Dependencies;

namespace TrueMoon.Extensions.DependencyInjection;

public static class DependencyDescriptorExtensions
{
    public static bool TryGetInstance(this IDependencyDescriptor descriptor, out object? instance)
    {
        var type = descriptor.GetType();
        var prop = type.GetProperty(nameof(IDependencyDescriptor<object>.Instance));
        instance = prop?.GetValue(descriptor);
        return instance != null;
    }
    
    public static bool TryGetFactory(this IDependencyDescriptor descriptor, out Func<IServiceProvider, object>? factory)
    {
        var type = descriptor.GetType();
        var prop = type.GetProperty(nameof(IDependencyDescriptor<object>.Factory));
        var value = prop?.GetValue(descriptor);
            
        if (value == null)
        {
            factory = default;
            return false;
        }

        if (value is not Delegate delegate1)
        {
            factory = default;
            return false;
        }

        factory = provider => delegate1.DynamicInvoke(provider)!;
        return true;
    }
}