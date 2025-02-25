using System.Collections.Immutable;
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
            .Where(item => item is not null)
            .Collect();

        var r = context.CompilationProvider.Combine(data);
        
        context.RegisterSourceOutput(r,static (sourceProductionContext, d) => Generate2(d.Left, d.Right, sourceProductionContext));
    }

    private static void Generate2(Compilation compilation, ImmutableArray<GenericNameSyntax> input,
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

            var serviceNameIndex = 0;
            var list = new List<(string service, ResolvingServiceCreationType creationType, string? factoryCode)>();
            foreach (var syntax in values)
            {
                SemanticModel semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);  
                
                var (lifetime, creationType, type) = GetDetails(syntax);
                
                if (semanticModel.GetSymbolInfo(syntax).Symbol is IMethodSymbol methodSymbol)
                {
                    bool isCompositeAdditions = false;
                    (string name, string code) generated = default;
                    
                    if (syntax.TypeArgumentList.Arguments.Count == 1 
                        && creationType is ResolvingServiceCreationType.Instance
                        && methodSymbol.Parameters.Length == 1)
                    {
                        var serviceTypeSymbol = GetTypeSymbol(semanticModel,syntax.TypeArgumentList.Arguments[0]);

                        generated = GenerateTypeResolver(assembly, lifetime, ResolvingServiceCreationType.Instance, serviceTypeSymbol!);
                    }
                    else if (syntax.TypeArgumentList.Arguments.Count == 1 
                        && creationType == null
                        && methodSymbol.Parameters.Length == 1)
                    {
                        var serviceTypeSymbol = GetTypeSymbol(semanticModel,syntax.TypeArgumentList.Arguments[0]);
                        creationType = ResolvingServiceCreationType.Factory;

                        generated = GenerateTypeResolver(assembly, lifetime, ResolvingServiceCreationType.Factory, serviceTypeSymbol!);
                    }
                    else if (syntax.TypeArgumentList.Arguments.Count == 2 && type == ResolvingServiceType.OpenGeneric)
                    {
                        var serviceTypeSymbol = GetTypeSymbol(semanticModel,syntax.TypeArgumentList.Arguments[0]);
                        var implementationTypeSymbol = GetTypeSymbol(semanticModel,syntax.TypeArgumentList.Arguments[1]);

                        generated = GenerateTypeResolver(assembly, lifetime, creationType ?? ResolvingServiceCreationType.New, serviceTypeSymbol!, implementationTypeSymbol);
                    }
                    else if (syntax.TypeArgumentList.Arguments.Count == 2)
                    {
                        var serviceTypeSymbol = GetTypeSymbol(semanticModel,syntax.TypeArgumentList.Arguments[0]);
                        var implementationTypeSymbol = GetTypeSymbol(semanticModel,syntax.TypeArgumentList.Arguments[1]);
                
                        generated = GenerateTypeResolver(assembly, lifetime, creationType ?? ResolvingServiceCreationType.New, serviceTypeSymbol!, implementationTypeSymbol);
                    }
                    else if (syntax.TypeArgumentList.Arguments.Count > 2 && type == ResolvingServiceType.Composite)
                    {
                        var serviceTypeSymbol = GetTypeSymbol(semanticModel,syntax.TypeArgumentList.Arguments[0]);

                        generated = GenerateTypeResolver(assembly, lifetime, ResolvingServiceCreationType.New, serviceTypeSymbol!);
                        
                        isCompositeAdditions = true;
                    }

                    sourceProductionContext.AddSource($"{generated.name}{serviceNameIndex}.g.cs", generated.code);
                        
                    list.Add((generated.name, creationType ?? ResolvingServiceCreationType.New, null));

                    serviceNameIndex++;
        
                    if (isCompositeAdditions)
                    {
                        var serviceTypeSymbolF = GetTypeSymbol(semanticModel,syntax.TypeArgumentList.Arguments[0]);
                        for (var i = 1; i < syntax.TypeArgumentList.Arguments.Count; i++)
                        {
                            var serviceTypeSymbol = GetTypeSymbol(semanticModel,syntax.TypeArgumentList.Arguments[i]);
                            var generated1 = GenerateTypeResolver(assembly, lifetime, ResolvingServiceCreationType.Factory, serviceTypeSymbol!);
                            
                            sourceProductionContext.AddSource($"{generated1.name}{serviceNameIndex}.g.cs", generated1.code);
                            
                            list.Add((generated1.name, ResolvingServiceCreationType.Factory, $"resolver => resolver.Resolve<global::{serviceTypeSymbolF}>()"));
                            serviceNameIndex++;
                        }
                    }
                    // if (list.Any(t=> SymbolEqualityComparer.Default.Equals(t.interfaceSymbol, interfaceSymbol)))
                    // {
                    //     continue;
                    // }
                }
            }
        
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
            foreach (var (service, creationType, factoryCode) in list)
            {
                var parameter = creationType switch
                {
                    ResolvingServiceCreationType.None => string.Empty,
                    ResolvingServiceCreationType.New => string.Empty,
                    ResolvingServiceCreationType.Instance => "a.GetInstance()",
                    ResolvingServiceCreationType.Factory when !string.IsNullOrWhiteSpace(factoryCode) => factoryCode,
                    ResolvingServiceCreationType.Factory => "a.GetFactory()",
                    _ => string.Empty
                };
                sb.AppendLine($"        ServiceResolvers.Shared.Add(a => new {service}({parameter}));");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            var source = sb.ToString();
            sourceProductionContext.AddSource("CobaltResolversRegistration.g.cs", source);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Exception - {e.GetType()}: {e.Message}, StackTrace: {e.StackTrace}", e);
        }

        return;

        INamedTypeSymbol? GetTypeSymbol(SemanticModel semanticModel, TypeSyntax syntax)
        {
            if (semanticModel.GetTypeInfo(syntax).Type is INamedTypeSymbol typeSymbol)
            {
                return typeSymbol;
            }

            return null;
        }
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
            "OpenSingleton" => (ResolvingServiceLifetime.Singleton, ResolvingServiceCreationType.Factory,
                ResolvingServiceType.OpenGeneric),
            "OpenTransient" => (ResolvingServiceLifetime.Transient, ResolvingServiceCreationType.Factory,
                ResolvingServiceType.OpenGeneric),
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
            sb.AppendLine($"public class {implementation.Name}Resolver : IResolver<global::{service}, global::{implementation}>");

            className = $"{implementation.Name}Resolver";
        }
        else
        {
            sb.AppendLine($"public class {service.Name}Resolver : IResolver<global::{service}>");
            className = $"{service.Name}Resolver";
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
                sb.AppendLine($"        var {serviceName} = context.Resolve<global::{symbol.Type}>();");
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

    private static GenericNameSyntax? GetMethods(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.Node is GenericNameSyntax i && i.TypeArgumentList.Arguments.Any())
        {
            return i;
        }
        return null;
    }
    
    private static bool CheckMethods(
        SyntaxNode syntaxNode,
        CancellationToken cancellationToken) =>
        syntaxNode.IsKind(SyntaxKind.GenericName)
        && syntaxNode is GenericNameSyntax
        {
            Identifier.Text: "Singleton" or "Transient" or "Instance" or "Composite" or "OpenSingleton"
            or "OpenTransient",
        };
}

public enum ResolvingServiceLifetime
{
    None,
    Singleton,
    Transient,
}

public enum ResolvingServiceCreationType
{
    None,
    New,
    Instance,
    Factory
}

public enum ResolvingServiceType
{
    None,
    Service,
    Composite,
    OpenGeneric
}