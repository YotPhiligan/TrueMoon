using System.Reflection;

namespace TrueMoon.Cobalt.Tests;

public class TestGenericServiceResolver : IUnboundGenericResolver
{
    public bool IsServiceDisposable { get; }
    public ResolvingServiceLifetime ServiceLifetime { get; }
    
    private static readonly Type UnboundGenericType = typeof(TestGenericService<>); 
    
    private readonly Dictionary<Type, object> _instances = new ();
    
    public object ResolveGeneric(Type[] genericArgument, IResolvingContext context)
    {
        var type = UnboundGenericType.MakeGenericType(genericArgument);
        
        if (ServiceLifetime == ResolvingServiceLifetime.Singleton 
            && _instances.TryGetValue(type, out var value))
        {
            return value;
        }
        
        object[] args = [
            context.Resolve<IService1>(),
            context.Resolve<SubService2>(),
        ];

        var ctrs = type.GetConstructors();
        
        var ctr = ctrs.First(t=>t.GetParameters().Length == args.Length);
        var item = ctr.Invoke(BindingFlags.Default, null, args, null);

        if (ServiceLifetime == ResolvingServiceLifetime.Singleton)
        {
            _instances.Add(type, item);
        }
        
        return item;
    }
}