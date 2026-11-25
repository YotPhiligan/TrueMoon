using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TrueMoon.Argentis.Generator;

/// <summary>Generates typed style role tokens from declarations on partial style classes.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ViewRolesGenerator : IIncrementalGenerator
{
    private const string AttributeName = "TrueMoon.Argentis.Styling.ViewRoleAttribute`1";
    private const string StyleName = "TrueMoon.Argentis.Styling.IViewStyle`1";
    private static readonly DiagnosticDescriptor InvalidStyle = new(
        "ARG009", "Unsupported view role declaration",
        "Style '{0}' must be a top-level, non-generic, non-file-local partial class implementing IViewStyle<TView>",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InvalidName = new(
        "ARG010", "Invalid view role name", "A generated view role must have a valid C# identifier as its name",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor NameConflict = new(
        "ARG011", "View role name conflict", "Style '{0}' cannot generate role '{1}' because that name already exists",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var styles = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName, static (_, _) => true, static (input, _) => ReadStyle(input));
        context.RegisterSourceOutput(styles, static (output, style) =>
        {
            if (style is null) return;
            foreach (var diagnostic in style.Diagnostics) output.ReportDiagnostic(diagnostic);
            if (style.Source is { } source) output.AddSource(style.HintName, source);
        });
    }

    private static GeneratedStyle? ReadStyle(GeneratorAttributeSyntaxContext input)
    {
        if (input.TargetSymbol is not INamedTypeSymbol style) return null;
        var roleAttribute = input.Attributes[0].AttributeClass!.OriginalDefinition;
        var attributes = style.GetAttributes();
        var first = attributes.First(attribute => IsRole(attribute, roleAttribute));
        // Analyze the style once, even when role attributes are spread over multiple partial declarations.
        var declaration = input.TargetNode;
        if (first.ApplicationSyntaxReference is not { } syntax || syntax.SyntaxTree != declaration.SyntaxTree ||
            !declaration.Span.Contains(syntax.Span)) return null;

        var hintName = $"{style.ToDisplayString()}.ViewRoles.g.cs";
        var styleInterface = input.SemanticModel.Compilation.GetTypeByMetadataName(StyleName);
        if (declaration is not ClassDeclarationSyntax type || !type.Modifiers.Any(SyntaxKind.PartialKeyword) ||
            style.IsGenericType || style.IsFileLocal || style.ContainingType is not null || styleInterface is null ||
            !style.AllInterfaces.Any(contract => SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, styleInterface)))
            return new(hintName, null, [Diagnostic.Create(InvalidStyle, Location.Create(syntax.SyntaxTree, syntax.Span), style.Name)]);

        var roles = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var attribute in attributes)
        {
            if (!IsRole(attribute, roleAttribute)) continue;
            var attributeSyntax = attribute.ApplicationSyntaxReference!;
            var location = Location.Create(attributeSyntax.SyntaxTree, attributeSyntax.Span);
            if (attribute.ConstructorArguments is not [{ Value: string name }] ||
                !SyntaxFacts.IsValidIdentifier(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
            {
                diagnostics.Add(Diagnostic.Create(InvalidName, location));
                continue;
            }
            if (name == style.Name || HasMember(style, name) || !roles.TryAdd(name,
                attribute.AttributeClass!.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                diagnostics.Add(Diagnostic.Create(NameConflict, location, style.Name, name));
        }

        return new(hintName, roles.Count == 0 ? null : Render(style, roles), diagnostics.ToImmutable());
    }

    private static bool IsRole(AttributeData attribute, INamedTypeSymbol roleAttribute) =>
        SymbolEqualityComparer.Default.Equals(attribute.AttributeClass?.OriginalDefinition, roleAttribute);

    private static bool HasMember(INamedTypeSymbol style, string name)
    {
        for (INamedTypeSymbol? current = style; current != null; current = current.BaseType)
            if (!current.GetMembers(name).IsEmpty) return true;
        return false;
    }

    private static string Render(INamedTypeSymbol style, SortedDictionary<string, string> roles)
    {
        var source = new StringBuilder("// <auto-generated/>\n#nullable enable\n\n");
        if (!style.ContainingNamespace.IsGlobalNamespace)
            source.Append("namespace ").Append(style.ContainingNamespace.ToDisplayString()).AppendLine(";").AppendLine();
        source.Append("partial class @").AppendLine(style.Name).AppendLine("{");
        foreach (var (name, type) in roles)
        {
            source.AppendLine("    /// <summary>Gets the shared token for this view style role.</summary>");
            source.Append("    internal static global::TrueMoon.Argentis.Styling.ViewStyleRole<").Append(type)
                .Append("> @").Append(name).Append(" { get; } = new(")
                .Append(SymbolDisplay.FormatLiteral(name, quote: true)).AppendLine(");");
        }
        return source.AppendLine("}").ToString();
    }

    private sealed record GeneratedStyle(string HintName, string? Source, ImmutableArray<Diagnostic> Diagnostics);
}
