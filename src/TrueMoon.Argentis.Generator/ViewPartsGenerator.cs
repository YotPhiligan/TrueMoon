using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TrueMoon.Argentis.Generator;

/// <summary>Generates typed named parts and inferred style roles from inline view content.</summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class ViewPartsGenerator : IIncrementalGenerator
{
    private const string ViewName = "TrueMoon.Argentis.Views.View";
    private static readonly DiagnosticDescriptor InvalidPart = new(
        "ARG006", "Unsupported generated view part",
        "A generated part requires a constant C# identifier and a control type that, along with all containing types, is public or internal, non-generic, non-file-local, and accessible from the generated class",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor DuplicateName = new(
        "ARG007", "Duplicate generated view part name",
        "View '{0}' declares more than one generated part named '{1}'",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor NameConflict = new(
        "ARG008", "Generated view part name conflict",
        "View '{0}' cannot generate '{1}' because that name already exists",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor UnsupportedFactory = new(
        "ARG022", "Unsupported view content declaration",
        "View '{0}' declares content the generator cannot analyze; use an inline lambda, a method of this view, or a call to a method of this view",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor ManualPartLookup = new(
        "ARG023", "Manual view part lookup",
        "View '{0}' looks up part '{1}' by name; the generated property '{2}' replaces it",
        "Argentis.Generation", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var views = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is TypeDeclarationSyntax type && IsCandidate(type),
            static (input, cancellation) => ReadView(input, cancellation));
        context.RegisterSourceOutput(views, static (output, view) =>
        {
            if (view is null) return;
            foreach (var diagnostic in view.Diagnostics) output.ReportDiagnostic(diagnostic);
            if (view.PartsSource is { } parts) output.AddSource($"{view.HintPrefix}.ViewParts.g.cs", parts);
            if (view.RolesSource is { } roles) output.AddSource($"{view.HintPrefix}.InferredViewRoles.g.cs", roles);
        });
    }

    private static bool IsCandidate(TypeDeclarationSyntax type) => type.Members.Any(static member =>
        Calls(member).Any(static call => IsContentCall(call) || IsSetContentCall(call)));

    private static bool IsContentCall(InvocationExpressionSyntax call) => call.Expression switch
    {
        IdentifierNameSyntax or GenericNameSyntax => ((SimpleNameSyntax)call.Expression).Identifier.ValueText is "Content" or "DeclareContent",
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax } member =>
            member.Name.Identifier.ValueText is "Content" or "DeclareContent",
        _ => false,
    };

    private static bool IsSetContentCall(InvocationExpressionSyntax call) => call.Expression switch
    {
        IdentifierNameSyntax name => name.Identifier.ValueText == "SetContent",
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } member =>
            member.Name.Identifier.ValueText == "SetContent",
        _ => false,
    };

    private static IEnumerable<InvocationExpressionSyntax> Calls(SyntaxNode root) => root
        .DescendantNodesAndSelf(static node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
        .OfType<InvocationExpressionSyntax>();

    private static GeneratedView? ReadView(GeneratorSyntaxContext input, CancellationToken cancellation)
    {
        var compilation = input.SemanticModel.Compilation;
        var view = compilation.GetTypeByMetadataName(ViewName);
        var declaration = (TypeDeclarationSyntax)input.Node;
        if (view is null || input.SemanticModel.GetDeclaredSymbol(declaration, cancellation) is not INamedTypeSymbol owner ||
            !SupportsExtensions(owner) || !DerivesFrom(owner, view)) return null;

        var declarations = owner.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax(cancellation))
            .OfType<TypeDeclarationSyntax>().ToArray();
        // Analyze each view once, even if multiple partial declarations are candidates.
        var first = declarations.First(IsCandidate);
        if (first.SyntaxTree != declaration.SyntaxTree || first.SpanStart != declaration.SpanStart)
            return null;
        var (factories, factoryDiagnostics) = ReadContentFactories(compilation, view, owner, declarations, cancellation);
        if (factories.Length == 0)
            return factoryDiagnostics.IsEmpty ? null : new GeneratedView(owner.ToDisplayString(), null, null, factoryDiagnostics);
        return ReadContent(owner, view, compilation, factories, factoryDiagnostics, declaration.GetLocation(), cancellation);
    }

    // Recognized content positions: an inline lambda, a method of this view, SetContent arguments in this
    // view's code, and one step into calls of this view's methods made from those positions.
    private static (ImmutableArray<SyntaxNode> Roots, ImmutableArray<Diagnostic> Diagnostics) ReadContentFactories(
        Compilation compilation, INamedTypeSymbol view, INamedTypeSymbol owner,
        IReadOnlyList<TypeDeclarationSyntax> declarations, CancellationToken cancellation)
    {
        var typedView = compilation.GetTypeByMetadataName("TrueMoon.Argentis.Views.View`1");
        var typedWindow = compilation.GetTypeByMetadataName("TrueMoon.Argentis.Windows.Window`1");
        var contentControl = compilation.GetTypeByMetadataName("TrueMoon.Argentis.Layout.ContentControl");
        var roots = ImmutableArray.CreateBuilder<SyntaxNode>();
        var seen = new HashSet<SyntaxNode>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        void AddRoot(SyntaxNode? node)
        {
            if (node is null || !seen.Add(node)) return;
            roots.Add(node);
        }

        foreach (var declaration in declarations)
        {
            var model = compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var call in declaration.Members.OfType<ConstructorDeclarationSyntax>().SelectMany(Calls).Where(IsContentCall))
            {
                // The body can already reference generated parts and roles that do not exist yet.
                var symbol = model.GetSymbolInfo(call, cancellation);
                var arguments = call.ArgumentList.Arguments;
                if (symbol.Symbol is IMethodSymbol method)
                {
                    if (!IsFrameworkContent(method, arguments.Count)) continue;
                }
                else
                {
                    var candidates = symbol.CandidateSymbols.IsEmpty
                        ? model.GetMemberGroup(call.Expression, cancellation) : symbol.CandidateSymbols;
                    if (candidates.OfType<IMethodSymbol>().Count(item => item.Parameters.Length == arguments.Count) != 1 ||
                        !candidates.OfType<IMethodSymbol>().Any(item => IsFrameworkContent(item, arguments.Count))) continue;
                }

                var argument = arguments.FirstOrDefault(static arg => arg.NameColon?.Name.Identifier.ValueText == "factory")
                    ?? arguments[arguments.Count - 1];
                if (argument.NameColon is { Name.Identifier.ValueText: not "factory" }) continue;
                var expression = argument.Expression;
                while (expression is ParenthesizedExpressionSyntax grouping) expression = grouping.Expression;
                if (expression is AnonymousFunctionExpressionSyntax factory) AddRoot(factory.Body);
                else if (OwnerMethod(model, expression, owner, cancellation) is { } named) AddRoot(MethodBody(named, cancellation));
                else diagnostics.Add(Diagnostic.Create(UnsupportedFactory, call.GetLocation(), owner.Name));
            }

            if (contentControl is not null)
                foreach (var call in declaration.Members.SelectMany(Calls).Where(IsSetContentCall))
                {
                    if (model.GetSymbolInfo(call, cancellation).Symbol is not IMethodSymbol setting ||
                        !SymbolEqualityComparer.Default.Equals(setting.ContainingType, contentControl)) continue;
                    if (call.ArgumentList.Arguments is [{ Expression: var content }] && content is not LiteralExpressionSyntax)
                        AddRoot(content);
                }
        }

        var declaredCount = roots.Count;
        for (var index = 0; index < declaredCount; index++)
        {
            var root = roots[index];
            var model = compilation.GetSemanticModel(root.SyntaxTree);
            foreach (var call in Calls(root))
                if (OwnerMethod(model, call.Expression, owner, cancellation) is { } callee)
                    AddRoot(MethodBody(callee, cancellation));
        }
        return (roots.ToImmutable(), diagnostics.ToImmutable());

        bool IsFrameworkContent(IMethodSymbol method, int arguments)
        {
            if (method.Parameters.Length != arguments) return false;
            var declaringType = method.ContainingType.OriginalDefinition;
            return SymbolEqualityComparer.Default.Equals(declaringType, view) ||
                SymbolEqualityComparer.Default.Equals(declaringType, typedView) ||
                SymbolEqualityComparer.Default.Equals(declaringType, typedWindow);
        }
    }

    private static IMethodSymbol? OwnerMethod(SemanticModel model, ExpressionSyntax expression, INamedTypeSymbol owner,
        CancellationToken cancellation)
    {
        var info = model.GetSymbolInfo(expression, cancellation);
        foreach (var candidate in info.Symbol is null ? info.CandidateSymbols : ImmutableArray.Create(info.Symbol))
            if (candidate is IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.LocalFunction } method &&
                SymbolEqualityComparer.Default.Equals(method.ContainingType, owner))
                return method;
        return null;
    }

    private static SyntaxNode? MethodBody(IMethodSymbol method, CancellationToken cancellation)
    {
        foreach (var reference in method.DeclaringSyntaxReferences)
            switch (reference.GetSyntax(cancellation))
            {
                case MethodDeclarationSyntax declaration:
                    if (declaration.Body is not null) return declaration.Body;
                    if (declaration.ExpressionBody is { } arrow) return arrow.Expression;
                    break;
                case LocalFunctionStatementSyntax local:
                    if (local.Body is not null) return local.Body;
                    if (local.ExpressionBody is { } localArrow) return localArrow.Expression;
                    break;
            }
        return null;
    }

    private static bool SupportsExtensions(INamedTypeSymbol owner) => owner is
    {
        ContainingType: null, IsGenericType: false, IsFileLocal: false,
        DeclaredAccessibility: Accessibility.Public or Accessibility.Internal
    };

    private static GeneratedView ReadContent(INamedTypeSymbol owner, INamedTypeSymbol view, Compilation compilation,
        IEnumerable<SyntaxNode> declarations, ImmutableArray<Diagnostic> factoryDiagnostics, Location location,
        CancellationToken cancellation)
    {
        var elementExtensions = compilation.GetTypeByMetadataName("TrueMoon.Argentis.ElementExtensions");
        var buildExtensions = compilation.GetTypeByMetadataName("TrueMoon.Argentis.Views.UiBuildExtensions");
        var wrapped = compilation.GetTypeByMetadataName("TrueMoon.Argentis.Views.UiControl`2");
        var extensionType = owner.Name + "Parts";
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>(factoryDiagnostics.Length);
        diagnostics.AddRange(factoryDiagnostics);
        var parts = new Dictionary<string, (INamedTypeSymbol Type, Accessibility Accessibility, bool IsOptional)>(StringComparer.Ordinal);
        var canGenerateParts = elementExtensions is not null;
        if (canGenerateParts && owner.ContainingNamespace.GetTypeMembers(extensionType).Any(type => type.Arity == 0))
        {
            diagnostics.Add(Diagnostic.Create(NameConflict, location, owner.Name, extensionType));
            canGenerateParts = false;
        }
        var roleInference = new InferredRoles(compilation, owner, buildExtensions, wrapped);
        foreach (var declaration in declarations)
        {
            var model = compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var call in Calls(declaration))
            {
                if (call.Expression is not MemberAccessExpressionSyntax member) continue;
                var methodName = member.Name.Identifier.ValueText;
                switch (methodName)
                {
                    case "WithRole":
                        roleInference.Read(model, call, member, diagnostics, cancellation);
                        continue;
                    case "Named" or "NamedOptional" when canGenerateParts:
                        break;
                    default:
                        continue;
                }

                var symbol = model.GetSymbolInfo(call, cancellation).Symbol ??
                    roleInference.BindAfterRole(model, call, member, cancellation);
                if (symbol is not IMethodSymbol { ReturnType: INamedTypeSymbol returnType } method)
                    continue;

                INamedTypeSymbol? partType;
                var declaringType = (method.ReducedFrom ?? method).ContainingType;
                if (wrapped is not null && buildExtensions is not null && SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, wrapped) &&
                    (SymbolEqualityComparer.Default.Equals(declaringType, buildExtensions) ||
                     SymbolEqualityComparer.Default.Equals(declaringType.ContainingType, buildExtensions)))
                    partType = returnType.TypeArguments[0] as INamedTypeSymbol;
                else if (method.ReducedFrom is not null && SymbolEqualityComparer.Default.Equals(declaringType, elementExtensions))
                    partType = returnType;
                else
                    continue;

                var accessibility = GetPartAccessibility(partType, compilation);
                if (call.ArgumentList.Arguments is not [{ Expression: var argument }] ||
                    model.GetConstantValue(argument, cancellation) is not { HasValue: true, Value: string name } ||
                    !SyntaxFacts.IsValidIdentifier(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
                    partType is null || accessibility == Accessibility.NotApplicable)
                {
                    diagnostics.Add(Diagnostic.Create(InvalidPart, call.GetLocation()));
                    continue;
                }

                if (HasMember(owner, name))
                    diagnostics.Add(Diagnostic.Create(NameConflict, call.GetLocation(), owner.Name, name));
                else if (!parts.TryAdd(name, (partType, accessibility, methodName == "NamedOptional")))
                    diagnostics.Add(Diagnostic.Create(DuplicateName, call.GetLocation(), owner.Name, name));
            }
        }

        if (canGenerateParts && parts.Count != 0)
            ReadManualLookups(owner, view, compilation, parts, diagnostics, cancellation);

        var source = parts.Count == 0 ? null : Render(owner, extensionType, parts);
        return new(owner.ToDisplayString(), source, roleInference.Render(), diagnostics.ToImmutable());
    }

    // A view consumes its generated parts; a manual lookup by name hides the generated property.
    private static void ReadManualLookups(INamedTypeSymbol owner, INamedTypeSymbol view, Compilation compilation,
        Dictionary<string, (INamedTypeSymbol Type, Accessibility Accessibility, bool IsOptional)> parts,
        ImmutableArray<Diagnostic>.Builder diagnostics, CancellationToken cancellation)
    {
        foreach (var reference in owner.DeclaringSyntaxReferences)
            if (reference.GetSyntax(cancellation) is TypeDeclarationSyntax declaration)
                foreach (var member in declaration.Members)
                {
                    var model = compilation.GetSemanticModel(member.SyntaxTree);
                    foreach (var call in Calls(member))
                    {
                        var lookup = call.Expression switch
                        {
                            IdentifierNameSyntax or GenericNameSyntax => ((SimpleNameSyntax)call.Expression).Identifier.ValueText,
                            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } accessor => accessor.Name.Identifier.ValueText,
                            _ => null,
                        };
                        if (lookup is not ("GetRequiredPart" or "TryGetPart")) continue;
                        if (model.GetSymbolInfo(call, cancellation).Symbol is not IMethodSymbol method ||
                            !SymbolEqualityComparer.Default.Equals((method.ReducedFrom ?? method).ContainingType, view)) continue;
                        if (call.ArgumentList.Arguments is not [{ Expression: var argument }, ..] ||
                            model.GetConstantValue(argument, cancellation) is not { HasValue: true, Value: string name } ||
                            !parts.ContainsKey(name)) continue;
                        diagnostics.Add(Diagnostic.Create(ManualPartLookup, call.GetLocation(), owner.Name, name, name));
                    }
                }
    }

    private static Accessibility GetPartAccessibility(INamedTypeSymbol? type, Compilation compilation)
    {
        if (type is null) return Accessibility.NotApplicable;

        var accessibility = Accessibility.Public;
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current is not
                {
                    TypeKind: not TypeKind.Error, IsGenericType: false, IsFileLocal: false,
                    DeclaredAccessibility: Accessibility.Public or Accessibility.Internal
                })
                return Accessibility.NotApplicable;
            if (current.DeclaredAccessibility == Accessibility.Internal)
                accessibility = Accessibility.Internal;
        }

        return compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)
            ? accessibility : Accessibility.NotApplicable;
    }

    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
        return false;
    }

    private static bool HasMember(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            if (current.GetMembers(name).Length != 0) return true;
        return false;
    }

    private static string Render(INamedTypeSymbol owner, string extensionType,
        Dictionary<string, (INamedTypeSymbol Type, Accessibility Accessibility, bool IsOptional)> parts)
    {
        var source = new StringBuilder("// <auto-generated/>\n#nullable enable\n\n");
        if (!owner.ContainingNamespace.IsGlobalNamespace)
            source.Append("namespace ").Append(owner.ContainingNamespace.ToDisplayString()).AppendLine(";").AppendLine();

        var access = owner.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        source.AppendLine("/// <summary>Provides typed access to the named parts of this view.</summary>");
        source.Append(access).Append(" static class ").AppendLine(extensionType).AppendLine("{");
        source.Append("    extension(").Append(owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .AppendLine(" view)").AppendLine("    {");
        foreach (var (name, part) in parts.OrderBy(part => part.Key, StringComparer.Ordinal))
        {
            var type = part.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var partAccess = part.Accessibility == Accessibility.Public ? "public" : "internal";
            var literal = SymbolDisplay.FormatLiteral(name, quote: true);
            source.AppendLine(part.IsOptional
                ? "        /// <summary>Gets the live named part in this view's scope, or null if absent.</summary>"
                : "        /// <summary>Gets the live named part in this view's scope.</summary>");
            source.Append("        ").Append(partAccess).Append(' ').Append(type);
            if (part.IsOptional)
                source.Append("? ").Append(name).Append(" => view.TryGetPart<").Append(type).Append(">(")
                    .Append(literal).AppendLine(", out var part) ? part : null;");
            else
                source.Append(' ').Append(name).Append(" => view.GetRequiredPart<").Append(type).Append(">(")
                    .Append(literal).AppendLine(");");
        }
        return source.AppendLine("    }").AppendLine("}").ToString();
    }

    private sealed record GeneratedView(string HintPrefix, string? PartsSource, string? RolesSource, ImmutableArray<Diagnostic> Diagnostics);
}
