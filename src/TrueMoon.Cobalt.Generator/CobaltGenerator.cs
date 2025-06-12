using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TrueMoon.Cobalt.Generator;

[Generator(LanguageNames.CSharp)]
public class CobaltGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var data = context.SyntaxProvider
            .CreateSyntaxProvider(CheckMethods, GetMethods)
            .Where(item => item != null)
            .Collect();

        var r = context.CompilationProvider.Combine(data);
        
        context.RegisterSourceOutput(r,static (sourceProductionContext, d) => Generate2(d.Left, d.Right, sourceProductionContext));
    }

    private static void Generate2(Compilation compilation, ImmutableArray<InvocationExpressionSyntax> input,
        SourceProductionContext sourceProductionContext)
    {
        if (input.IsDefaultOrEmpty)
        {
            return;
        }

        try
        {
            var values = input.Distinct();

            var b = compilation.GetDiagnostics();
            
            var assembly = compilation.Assembly.Name;
            
            var list = new List<ResolverSourceItem>();
            foreach (var invocationExpressionSyntax in values)
            {
                var semanticModel = compilation.GetSemanticModel(invocationExpressionSyntax.SyntaxTree);
                
                if (semanticModel.GetSymbolInfo(invocationExpressionSyntax).Symbol is IMethodSymbol methodSymbol)
                {
                    var returnTypeStr = $"{methodSymbol.ReturnType}";
                    if (!returnTypeStr.Contains("ITestInterface") 
                        && !returnTypeStr.Contains("IServicesRegistrationContext"))
                    {
                        continue;
                    }
                    
                    ResolverSourceItem item = null;
                    switch (methodSymbol.Name)
                    {
                        case "Instance":
                        {
                            INamedTypeSymbol serviceTypeSymbol = null;
                            if (methodSymbol.TypeArguments.Length == 1)
                            {
                                // Instance<T>(obj)
                                serviceTypeSymbol = methodSymbol.TypeArguments[0] as INamedTypeSymbol;
                            }
                            else
                            {
                                // Instance(obj)
                                serviceTypeSymbol = methodSymbol.Parameters[0].Type as INamedTypeSymbol;
                            }

                            var generated = GenerateTypeResolver(assembly, ResolvingServiceLifetime.Singleton, ResolvingServiceCreationType.Instance, serviceTypeSymbol!);

                            item = new ResolverSourceItem
                            {
                                ServiceName = generated.name,
                                CreationType = ResolvingServiceCreationType.Instance,
                                ServiceTypeSymbol = serviceTypeSymbol,
                                Source = generated.code
                            };
                        }
                            break;
                        case "Singleton":
                        {
                            if (methodSymbol.TypeArguments.Length == 1)
                            {
                                if (methodSymbol.Parameters.Length == 1)
                                {
                                    // Singleton<TService>(resolver => resolver.Resolve<object>)
                                    var serviceTypeSymbol = methodSymbol.TypeArguments[0] as INamedTypeSymbol;

                                    var generated = GenerateTypeResolver(assembly, ResolvingServiceLifetime.Singleton, ResolvingServiceCreationType.Factory, serviceTypeSymbol!);
                                    
                                    item = new ResolverSourceItem
                                    {
                                        ServiceName = generated.name,
                                        CreationType = ResolvingServiceCreationType.Factory,
                                        ServiceTypeSymbol = serviceTypeSymbol,
                                        Source = generated.code,
                                        FactoryCode = $"resolver => resolver.Resolve<global::{serviceTypeSymbol}>()"
                                    };
                                }
                                else
                                {
                                    // Singleton<TService>()
                                    var serviceTypeSymbol = methodSymbol.TypeArguments[0] as INamedTypeSymbol;

                                    var generated = GenerateTypeResolver(assembly, ResolvingServiceLifetime.Singleton, ResolvingServiceCreationType.New, serviceTypeSymbol!);
                                    
                                    item = new ResolverSourceItem
                                    {
                                        ServiceName = generated.name,
                                        CreationType = ResolvingServiceCreationType.New,
                                        ServiceTypeSymbol = serviceTypeSymbol,
                                        Source = generated.code
                                    };
                                }
                            }
                            else if (methodSymbol.TypeArguments.Length == 2)
                            {
                                // Singleton<TService,TImplementation>()
                                var serviceTypeSymbol = methodSymbol.TypeArguments[0] as INamedTypeSymbol;
                                var implementationTypeSymbol = methodSymbol.TypeArguments[1] as INamedTypeSymbol;

                                var generated = GenerateTypeResolver(assembly, ResolvingServiceLifetime.Singleton, ResolvingServiceCreationType.New, serviceTypeSymbol!, implementationTypeSymbol);
                                    
                                item = new ResolverSourceItem
                                {
                                    ServiceName = generated.name,
                                    CreationType = ResolvingServiceCreationType.New,
                                    ServiceTypeSymbol = serviceTypeSymbol,
                                    ImplementationTypeSymbol = implementationTypeSymbol,
                                    Source = generated.code
                                };
                            }
                            else if (methodSymbol.TypeArguments.IsEmpty)
                            {
                                //Debug.Assert(serviceTypeExpression != null, $"serviceTypeExpression == null: {invocationExpressionSyntax}");
                                // Singleton(typeof(ServiceType),typeof(ImplementationType))
                                if (methodSymbol.Parameters.Length == 2)
                                {
                                    INamedTypeSymbol? serviceTypeSymbol = null;
                                    INamedTypeSymbol? implementationTypeSymbol = null;
                                
                                    var serviceTypeExpression = invocationExpressionSyntax.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
                                    var serviceType = serviceTypeExpression?.Type;
                                    
                                    if (serviceType != null && semanticModel.GetSymbolInfo(serviceType).Symbol is INamedTypeSymbol symbol)
                                    {
                                        serviceTypeSymbol = symbol;
                                    }
                                    else
                                    {
                                        Debug.Print($"{serviceTypeExpression} is not TypeOfExpressionSyntax");
                                        continue;
                                    }
                                    
                                    var implementationTypeExpression = invocationExpressionSyntax.ArgumentList.Arguments[1].Expression as TypeOfExpressionSyntax;
                                    var implementationType = implementationTypeExpression?.Type;
                                
                                    if (implementationType != null && semanticModel.GetSymbolInfo(implementationType).Symbol is INamedTypeSymbol symbol2)
                                    {
                                        implementationTypeSymbol = symbol2;
                                    }
                                    else
                                    {
                                        Debug.Print($"{implementationTypeExpression} is not TypeOfExpressionSyntax");
                                        continue;
                                    }
                        
                                    var generated = GenerateUnboundGenericResolver(assembly, ResolvingServiceLifetime.Singleton, serviceTypeSymbol!, implementationTypeSymbol);

                                    item = new ResolverSourceItem
                                    {
                                        ServiceName = generated.name,
                                        CreationType = ResolvingServiceCreationType.Generic,
                                        ServiceTypeSymbol = serviceTypeSymbol,
                                        AdditionalType = implementationTypeSymbol,
                                        Source = generated.code
                                    };
                                }
                                else if (methodSymbol.Parameters.Length == 1)
                                {
                                    // Singleton(typeof(ServiceType))
                                    INamedTypeSymbol? serviceTypeSymbol = null;
                                
                                    var serviceTypeExpression = invocationExpressionSyntax.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
                                    var serviceType = serviceTypeExpression?.Type;
                                    
                                    if (serviceType != null && semanticModel.GetSymbolInfo(serviceType).Symbol is INamedTypeSymbol symbol)
                                    {
                                        serviceTypeSymbol = symbol;
                                    }
                                    else
                                    {
                                        Debug.Print($"{serviceTypeExpression} is not TypeOfExpressionSyntax");
                                        continue;
                                    }
                        
                                    var generated = GenerateUnboundGenericResolver(assembly, ResolvingServiceLifetime.Singleton, serviceTypeSymbol!);

                                    item = new ResolverSourceItem
                                    {
                                        ServiceName = generated.name,
                                        CreationType = ResolvingServiceCreationType.Generic,
                                        ServiceTypeSymbol = serviceTypeSymbol,
                                        Source = generated.code
                                    };
                                }
                            }
                        }
                            break;
                        case "Transient":
                        {
                            if (methodSymbol.TypeArguments.Length == 1)
                            {
                                if (methodSymbol.Parameters.Length == 1)
                                {
                                    // Transient<TService>(resolver => resolver.Resolve<object>)
                                    var serviceTypeSymbol = methodSymbol.TypeArguments[0] as INamedTypeSymbol;

                                    var generated = GenerateTypeResolver(assembly, ResolvingServiceLifetime.Transient, ResolvingServiceCreationType.Factory, serviceTypeSymbol!);
                                    
                                    item = new ResolverSourceItem
                                    {
                                        ServiceName = generated.name,
                                        CreationType = ResolvingServiceCreationType.Factory,
                                        ServiceTypeSymbol = serviceTypeSymbol,
                                        Source = generated.code,
                                        FactoryCode = $"resolver => resolver.Resolve<global::{serviceTypeSymbol}>()"
                                    };
                                }
                                else
                                {
                                    // Transient<TService>()
                                    var serviceTypeSymbol = methodSymbol.TypeArguments[0] as INamedTypeSymbol;

                                    var generated = GenerateTypeResolver(assembly, ResolvingServiceLifetime.Transient, ResolvingServiceCreationType.New, serviceTypeSymbol!);
                                    
                                    item = new ResolverSourceItem
                                    {
                                        ServiceName = generated.name,
                                        CreationType = ResolvingServiceCreationType.New,
                                        ServiceTypeSymbol = serviceTypeSymbol,
                                        Source = generated.code
                                    };
                                }
                            }
                            else if (methodSymbol.TypeArguments.Length == 2)
                            {
                                // Singleton<TService,TImplementation>()
                                var serviceTypeSymbol = methodSymbol.TypeArguments[0] as INamedTypeSymbol;
                                var implementationTypeSymbol = methodSymbol.TypeArguments[1] as INamedTypeSymbol;

                                var generated = GenerateTypeResolver(assembly, ResolvingServiceLifetime.Transient, ResolvingServiceCreationType.New, serviceTypeSymbol!, implementationTypeSymbol);
                                    
                                item = new ResolverSourceItem
                                {
                                    ServiceName = generated.name,
                                    CreationType = ResolvingServiceCreationType.New,
                                    ServiceTypeSymbol = serviceTypeSymbol,
                                    ImplementationTypeSymbol = implementationTypeSymbol,
                                    Source = generated.code
                                };
                            }
                            else if (methodSymbol.TypeArguments.IsEmpty)
                            {
                                // Transient(typeof(ServiceType), typeof(ImplementationType))
                                if (methodSymbol.Parameters.Length == 2)
                                {
                                    INamedTypeSymbol? serviceTypeSymbol = null;
                                    INamedTypeSymbol? implementationTypeSymbol = null;
                                
                                    var serviceTypeExpression = invocationExpressionSyntax.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
                                    var serviceType = serviceTypeExpression?.Type;
                                    
                                    if (serviceType != null && semanticModel.GetSymbolInfo(serviceType).Symbol is INamedTypeSymbol symbol)
                                    {
                                        serviceTypeSymbol = symbol;
                                    }
                                    else
                                    {
                                        Debug.Print($"{serviceTypeExpression} is not TypeOfExpressionSyntax");
                                        continue;
                                    }
                                    
                                    var implementationTypeExpression = invocationExpressionSyntax.ArgumentList.Arguments[1].Expression as TypeOfExpressionSyntax;
                                    var implementationType = implementationTypeExpression?.Type;
                                
                                    if (implementationType != null && semanticModel.GetSymbolInfo(implementationType).Symbol is INamedTypeSymbol symbol2)
                                    {
                                        implementationTypeSymbol = symbol2;
                                    }
                                    else
                                    {
                                        Debug.Print($"{implementationTypeExpression} is not TypeOfExpressionSyntax");
                                        continue;
                                    }
                        
                                    var generated = GenerateUnboundGenericResolver(assembly, ResolvingServiceLifetime.Transient, serviceTypeSymbol!, implementationTypeSymbol);

                                    item = new ResolverSourceItem
                                    {
                                        ServiceName = generated.name,
                                        CreationType = ResolvingServiceCreationType.Generic,
                                        ServiceTypeSymbol = serviceTypeSymbol,
                                        AdditionalType = implementationTypeSymbol,
                                        Source = generated.code
                                    };
                                }
                                else if (methodSymbol.Parameters.Length == 1)
                                {
                                    // Transient(typeof(ServiceType))
                                    INamedTypeSymbol? serviceTypeSymbol = null;
                                
                                    var serviceTypeExpression = invocationExpressionSyntax.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
                                    var serviceType = serviceTypeExpression?.Type;
                                    
                                    if (serviceType != null && semanticModel.GetSymbolInfo(serviceType).Symbol is INamedTypeSymbol symbol)
                                    {
                                        serviceTypeSymbol = symbol;
                                    }
                                    else
                                    {
                                        Debug.Print($"{serviceTypeExpression} is not TypeOfExpressionSyntax");
                                        continue;
                                    }
                        
                                    var generated = GenerateUnboundGenericResolver(assembly, ResolvingServiceLifetime.Transient, serviceTypeSymbol!);

                                    item = new ResolverSourceItem
                                    {
                                        ServiceName = generated.name,
                                        CreationType = ResolvingServiceCreationType.Generic,
                                        ServiceTypeSymbol = serviceTypeSymbol,
                                        Source = generated.code
                                    };
                                }
                            }
                        }
                            break;
                        case "Composite":
                        {
                            if (methodSymbol.TypeArguments.Length < 3)
                            {
                                continue;
                            }

                            var serviceTypeSymbol = methodSymbol.TypeArguments[0] as INamedTypeSymbol;
                            
                            var generated = GenerateTypeResolver(assembly, ResolvingServiceLifetime.Singleton, ResolvingServiceCreationType.New, serviceTypeSymbol!);
                        
                            item = new ResolverSourceItem
                            {
                                ServiceName = generated.name,
                                CreationType = ResolvingServiceCreationType.New,
                                AdditionalType = serviceTypeSymbol,
                                Source = generated.code
                            };
                            
                            for (var i = 1; i < methodSymbol.TypeArguments.Length; i++)
                            {
                                var additionalServiceTypeSymbol = methodSymbol.TypeArguments[i] as INamedTypeSymbol;
                                var generated1 = GenerateTypeResolver(assembly,ResolvingServiceLifetime.Singleton, ResolvingServiceCreationType.Factory, additionalServiceTypeSymbol!);

                                if (list.All(t => t.ServiceName != generated1.name))
                                {
                                    sourceProductionContext.AddSource($"{generated1.name}.g.cs", generated1.code);
                            
                                    list.Add(new ResolverSourceItem
                                    {
                                        ServiceName = generated1.name,
                                        CreationType = ResolvingServiceCreationType.Factory,
                                        FactoryCode = $"resolver => resolver.Resolve<global::{serviceTypeSymbol}>()"
                                    });
                                }
                            }
                        }
                            break;
                    }
                    
                    if (item != null && list.All(t => t.ServiceName != item.ServiceName))
                    {
                        list.Add(item);
                        sourceProductionContext.AddSource($"{item.ServiceName}.g.cs", item.Source);
                    }
                }
            }
        
            var source = GetResolversRegistrationSource(assembly, list);
            sourceProductionContext.AddSource("CobaltResolversRegistration.g.cs", source);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Exception - {e.GetType()}: {e.Message}, StackTrace: {e.StackTrace}", e);
        }
    }

    private static string GetResolversRegistrationSource(string assembly, List<ResolverSourceItem> list)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// auto-generated by TrueMoon.Cobalt.Generator");
        sb.AppendLine("using TrueMoon.Cobalt;");
        sb.AppendLine("using TrueMoon.Services;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine($"namespace {assembly}.Cobalt.Generated;");
        sb.AppendLine();
            
        sb.AppendLine("public static class CobaltResolversRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    public static void Init()");
        sb.AppendLine("    {");
        foreach (var item in list)
        {
            var vstr = item.CreationType switch
            {
                ResolvingServiceCreationType.New =>
                    $"        ServiceResolvers.Shared.Add(a => new {item.ServiceName}());",
                ResolvingServiceCreationType.Instance => $"        ServiceResolvers.Shared.Add(a => new {item.ServiceName}(a.GetInstance<global::{item.ServiceTypeSymbol}>()));",
                ResolvingServiceCreationType.Factory when !string.IsNullOrWhiteSpace(item.FactoryCode) => 
                    $"        ServiceResolvers.Shared.Add(a => new {item.ServiceName}({item.FactoryCode}));",
                ResolvingServiceCreationType.Factory => 
                    $"        ServiceResolvers.Shared.Add(a => new {item.ServiceName}(a.GetFactory<global::{item.ServiceTypeSymbol}>().Get()));",
                ResolvingServiceCreationType.Generic => 
                    $"        ServiceResolvers.Shared.Add(typeof(global::{item.ServiceTypeSymbol}), a => new {item.ServiceName}());",
                _ => $"        ServiceResolvers.Shared.Add(a => new {item.ServiceName}());",
            };

            sb.AppendLine(vstr);
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
            
        var source = sb.ToString();
        return source;
    }

    private static (ResolvingServiceLifetime lifetime, ResolvingServiceCreationType? creationType, ResolvingServiceType type) GetDetails(GenericNameSyntax syntax) =>
        syntax.Identifier.Text switch
        {
            "Singleton" => (ResolvingServiceLifetime.Singleton, null, ResolvingServiceType.Service),
            "Transient" => (ResolvingServiceLifetime.Transient, null, ResolvingServiceType.Service),
            "Instance" => (ResolvingServiceLifetime.Singleton, ResolvingServiceCreationType.Instance,
                ResolvingServiceType.Service),
            "Composite" => (ResolvingServiceLifetime.Singleton, ResolvingServiceCreationType.New,
                ResolvingServiceType.Composite),
            "GenericSingleton" => (ResolvingServiceLifetime.Singleton, ResolvingServiceCreationType.Factory,
                ResolvingServiceType.UnboundGeneric),
            "GenericTransient" => (ResolvingServiceLifetime.Transient, ResolvingServiceCreationType.Factory,
                ResolvingServiceType.UnboundGeneric),
            _ => (ResolvingServiceLifetime.None, null, ResolvingServiceType.None)
        };
    
    private static (string name, string code) GenerateTypeResolver(string assembly, ResolvingServiceLifetime lifetime,
        ResolvingServiceCreationType creationType, 
        INamedTypeSymbol service,
        INamedTypeSymbol? implementation = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// auto-generated by TrueMoon.Cobalt.Generator");
        sb.AppendLine("using TrueMoon.Services;");
        sb.AppendLine("using TrueMoon.Cobalt;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine($"namespace {assembly}.Cobalt.Generated;");
        sb.AppendLine();
        
        string className = default;
        if (implementation != null)
        {
            className = $"{implementation.Name}Resolver";
            sb.AppendLine($"public class {className} : IResolver<global::{service}, global::{implementation}>");
        }
        else
        {
            className = $"{service.Name}Resolver";
            sb.AppendLine($"public class {className} : IResolver<global::{service}>");
        }
        
        var resolvingType = implementation ?? service;
        
        var higherConstructor = resolvingType.InstanceConstructors
            .OrderByDescending(t => t.Parameters.Length)
            .FirstOrDefault();
        
        sb.AppendLine("{");
        
        if (lifetime == ResolvingServiceLifetime.Singleton)
        {
            sb.AppendLine($"    private global::{service} _instance;");
            sb.AppendLine();
        }
        
        switch (lifetime)
        {
            case ResolvingServiceLifetime.Singleton:
                sb.AppendLine("    public ResolvingServiceLifetime ServiceLifetime => ResolvingServiceLifetime.Singleton;");
                break;
            case ResolvingServiceLifetime.Transient:
                sb.AppendLine("    public ResolvingServiceLifetime ServiceLifetime => ResolvingServiceLifetime.Transient;");
                break;
        }

        sb.AppendLine();
        
        if (creationType == ResolvingServiceCreationType.Instance)
        {
            sb.AppendLine($"    public {service.Name}Resolver(global::{service} instance)");
            sb.AppendLine("    {");
            sb.AppendLine("        _instance = instance;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        else if (creationType == ResolvingServiceCreationType.Factory)
        {
            sb.AppendLine($"    private readonly Func<IServiceResolver,global::{service}> _factory; ");
            sb.AppendLine();
            sb.AppendLine($"    public {service.Name}Resolver(Func<IServiceResolver,global::{service}> factory)");
            sb.AppendLine("    {");
            sb.AppendLine("        _factory = factory;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine($"    public global::{service} Resolve(IResolvingContext context)");
        sb.AppendLine("    {");
        
        if (lifetime == ResolvingServiceLifetime.Singleton)
        {
            if (creationType != ResolvingServiceCreationType.Instance)
            {
                sb.AppendLine("        if(_instance != null) return _instance;");
                sb.AppendLine();
            }
        }
        
        var i = 0;
        var parametersString = string.Empty;
        if (higherConstructor != null)
        {
            foreach (var symbol in higherConstructor.Parameters)
            {
                var serviceName = $"service{i}";
                sb.AppendLine(symbol.NullableAnnotation == NullableAnnotation.Annotated
                    ? $"        var {serviceName} = context.TryResolve<global::{symbol.Type}>();"
                    : $"        var {serviceName} = context.Resolve<global::{symbol.Type}>();");
                parametersString += serviceName + ",";
                i++;
            }
        }

        if (lifetime == ResolvingServiceLifetime.Singleton)
        {
            if (creationType == ResolvingServiceCreationType.Factory)
            {
                sb.AppendLine("        _instance = _factory(context);");
            }
            else if (creationType == ResolvingServiceCreationType.New)
            {
                sb.AppendLine($"        _instance = new global::{resolvingType}({parametersString.TrimEnd(',')});");
            }

            sb.AppendLine("        return _instance;");
        }
        else
        {
            sb.AppendLine(creationType == ResolvingServiceCreationType.Factory
                ? "        return _factory(context);"
                : $"        return new global::{resolvingType}({parametersString.TrimEnd(',')});");
        }
        
        sb.AppendLine("    }");

        var isDisposable = resolvingType.AllInterfaces.Any(t=>t.Name is "IDisposable" or "IAsyncDisposable");
        
        sb.AppendLine();
        sb.AppendLine("    public bool IsServiceDisposable { get; } = " + (isDisposable ? "true" : "false") + ";");
        sb.AppendLine();
        sb.AppendLine("    object IResolver.Resolve(IResolvingContext context) => Resolve(context);");
        sb.AppendLine("}");

        var source = sb.ToString();

        return (className, source);
    }
    
    private static (string name, string code) GenerateUnboundGenericResolver(string assembly, ResolvingServiceLifetime lifetime,
        INamedTypeSymbol service,
        INamedTypeSymbol? implementation = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// auto-generated by TrueMoon.Cobalt.Generator");
        sb.AppendLine("using TrueMoon.Services;");
        sb.AppendLine("using TrueMoon.Cobalt;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();
        sb.AppendLine($"namespace {assembly}.Cobalt.Generated;");
        sb.AppendLine();
        
        string className = default;
        if (implementation != null)
        {
            sb.AppendLine($"public class {implementation.Name}Resolver : IUnboundGenericResolver");

            className = $"{implementation.Name}Resolver";
        }
        else
        {
            sb.AppendLine($"public class {service.Name}Resolver : IUnboundGenericResolver");
            className = $"{service.Name}Resolver";
        }
        
        var resolvingType = implementation ?? service;
        
        var higherConstructor = resolvingType.InstanceConstructors
            .OrderByDescending(t => t.Parameters.Length)
            .FirstOrDefault();
        
        sb.AppendLine("{");
        
        if (lifetime == ResolvingServiceLifetime.Singleton)
        {
            sb.AppendLine("    private readonly Dictionary<Type, object> _instances = new ();");
            sb.AppendLine();
        }
        
        switch (lifetime)
        {
            case ResolvingServiceLifetime.Singleton:
                sb.AppendLine("    public ResolvingServiceLifetime ServiceLifetime => ResolvingServiceLifetime.Singleton;");
                break;
            case ResolvingServiceLifetime.Transient:
                sb.AppendLine("    public ResolvingServiceLifetime ServiceLifetime => ResolvingServiceLifetime.Transient;");
                break;
        }

        sb.AppendLine();
        sb.AppendLine($"    private static readonly Type UnboundGenericType = typeof(global::{resolvingType});");
        
        sb.AppendLine("    public object ResolveGeneric(Type[] genericArgument, IResolvingContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        var type = UnboundGenericType.MakeGenericType(genericArgument);");
        sb.AppendLine();
        
        if (lifetime == ResolvingServiceLifetime.Singleton)
        {
            sb.AppendLine("        if(_instances.TryGetValue(type, out var value))");
            sb.AppendLine("        {");
            sb.AppendLine("            return value;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        sb.AppendLine("        object[] args = [");
        if (higherConstructor != null)
        {
            foreach (var symbol in higherConstructor.Parameters)
            {
                sb.AppendLine($"        context.Resolve<global::{symbol.Type}>(),");
            }
        }
        sb.AppendLine("        ];");
        sb.AppendLine("        var ctrs = type.GetConstructors();");
        sb.AppendLine("        var ctr = ctrs.First(t=>t.GetParameters().Length == args.Length);");
        sb.AppendLine("        var item = ctr.Invoke(BindingFlags.Default, null, args, null);");
        
        if (lifetime == ResolvingServiceLifetime.Singleton)
        {
            sb.AppendLine("        _instances.Add(type, item);");
        }
        
        sb.AppendLine("        return item;");
        
        sb.AppendLine("    }");

        var isDisposable = resolvingType.AllInterfaces.Any(t=>t.Name is "IDisposable" or "IAsyncDisposable");
        
        sb.AppendLine();
        sb.AppendLine("    public bool IsServiceDisposable { get; } = " + (isDisposable ? "true" : "false") + ";");
        sb.AppendLine();
        sb.AppendLine("}");

        var source = sb.ToString();

        return (className, source);
    }

    private static InvocationExpressionSyntax? GetMethods(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        // if (context.Node is GenericNameSyntax i && i.TypeArgumentList.Arguments.Any())
        // {
        //     return (i, null);
        // }

        // if (context.Node is InvocationExpressionSyntax invocation && invocation.ArgumentList.Arguments.Any())
        // {
        //     return (null, invocation);
        // }
        
        if (context.Node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax or IdentifierNameSyntax } } invocation)
        {
            return invocation;
        }
        
        return null;
    }
    
    private static bool CheckMethods(
        SyntaxNode syntaxNode,
        CancellationToken cancellationToken)

    {
        // if (syntaxNode.IsKind(SyntaxKind.GenericName))
        // {
        //     return syntaxNode is GenericNameSyntax
        //     {
        //         Identifier.Text: "Singleton" or "Transient" or "Instance" or "Composite",
        //     };
        // }
        
        if (syntaxNode.IsKind(SyntaxKind.InvocationExpression) && syntaxNode is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax m
            })
        {
            return m.Name.Identifier.Text is "Singleton" or "Transient" or "Instance" or "Composite";
        }
        return false;
    }
}