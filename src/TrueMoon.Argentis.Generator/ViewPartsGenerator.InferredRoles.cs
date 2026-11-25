using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TrueMoon.Argentis.Generator;

public sealed partial class ViewPartsGenerator
{
    private static readonly DiagnosticDescriptor IncompatibleRoleTypes = new(
        "ARG012", "Incompatible inferred view role types",
        "View '{0}' infers role '{1}' for both '{2}' and '{3}'; all uses must have the same control type",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);
    private static readonly DiagnosticDescriptor InvalidInferredRole = new(
        "ARG013", "Unsupported inferred view role",
        "View '{0}' cannot generate inferred role '{1}': {2}",
        "Argentis.Generation", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private sealed class InferredRoles(Compilation compilation, INamedTypeSymbol owner,
        INamedTypeSymbol? buildExtensions, INamedTypeSymbol? wrapped)
    {
        private const string RoleType = "global::TrueMoon.Argentis.Styling.ViewStyleRole";
        private readonly string _holder = owner.Name + "Roles";
        private readonly INamedTypeSymbol? _element = compilation.GetTypeByMetadataName("TrueMoon.Argentis.Element");
        private readonly INamedTypeSymbol? _styleExtensions = compilation.GetTypeByMetadataName("TrueMoon.Argentis.Styling.ViewStyleExtensions");
        private SortedDictionary<string, INamedTypeSymbol>? _roles;

        public void Read(SemanticModel model, InvocationExpressionSyntax call, MemberAccessExpressionSyntax member,
            ImmutableArray<Diagnostic>.Builder diagnostics, CancellationToken cancellation)
        {
            if (ReadReference(model, call, cancellation) is not { } reference ||
                ResolveRoleCall(model, call, member, cancellation) is not { } binding) return;

            var name = reference.Name.Identifier.ValueText;
            var control = binding.Control;
            if (name == _holder)
                diagnostics.Add(Diagnostic.Create(InvalidInferredRole, reference.GetLocation(), owner.Name, name,
                    "a role cannot have the same name as its generated class"));
            else if (!CanExpose(control))
                diagnostics.Add(Diagnostic.Create(InvalidInferredRole, reference.GetLocation(), owner.Name, name,
                    $"control type '{control}' is not accessible to the generated class"));
            else if (!(_roles ??= new(StringComparer.Ordinal)).TryAdd(name, control) &&
                !SymbolEqualityComparer.Default.Equals(_roles[name], control))
                diagnostics.Add(Diagnostic.Create(IncompatibleRoleTypes, reference.GetLocation(), owner.Name, name,
                    _roles[name].ToDisplayString(), control.ToDisplayString()));
        }

        public ISymbol? BindAfterRole(SemanticModel model, InvocationExpressionSyntax call, MemberAccessExpressionSyntax member,
            CancellationToken cancellation)
        {
            var receiver = RepairChain(model, member.Expression, cancellation);
            return receiver is null ? null : BindSpeculatively(model, call, call.WithExpression(member.WithExpression(receiver)));
        }

        private MemberAccessExpressionSyntax? ReadReference(SemanticModel model, InvocationExpressionSyntax call,
            CancellationToken cancellation)
        {
            if (call.ArgumentList.Arguments is not [{ Expression: var argument }]) return null;
            while (argument is ParenthesizedExpressionSyntax grouping) argument = grouping.Expression;
            if (argument is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax holder, Name: IdentifierNameSyntax name } reference ||
                holder.Identifier.ValueText != _holder || name.Identifier.IsMissing) return null;

            var symbol = model.GetSymbolInfo(holder, cancellation);
            return symbol.Symbol is null && symbol.CandidateSymbols.IsEmpty ? reference : null;
        }

        private (InvocationExpressionSyntax Call, INamedTypeSymbol Control)? ResolveRoleCall(SemanticModel model,
            InvocationExpressionSyntax call, MemberAccessExpressionSyntax member, CancellationToken cancellation)
        {
            // Do not turn a bound user method into a role declaration.
            if (model.GetSymbolInfo(call, cancellation).Symbol is IMethodSymbol bound && !IsFrameworkRole(bound)) return null;

            var receiver = RepairChain(model, member.Expression, cancellation);
            var receiverType = receiver is null
                ? model.GetTypeInfo(member.Expression, cancellation).Type
                : model.GetSpeculativeTypeInfo(member.Expression.SpanStart, receiver, SpeculativeBindingOption.BindAsExpression).Type;
            var control = GetControlType(receiverType);
            if (control is null) return null;

            // The unknown role argument prevents generic inference. Rebind with a typed stand-in, letting Roslyn
            // check which overload is actually in scope for the real receiver rather than guessing from syntax.
            var roleType = SyntaxFactory.ParseTypeName($"{RoleType}<{control.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>");
            var argument = call.ArgumentList.Arguments[0].WithExpression(SyntaxFactory.DefaultExpression(roleType));
            var probe = call.WithExpression(member.WithExpression(receiver ?? member.Expression))
                .WithArgumentList(call.ArgumentList.WithArguments(SyntaxFactory.SingletonSeparatedList(argument)));
            return BindSpeculatively(model, call, probe) is IMethodSymbol method && IsFrameworkRole(method) ? (probe, control) : null;
        }

        private ExpressionSyntax? RepairChain(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellation)
        {
            // Only follow the fluent receiver spine, not arbitrary arguments, handlers, or local functions.
            if (expression is ParenthesizedExpressionSyntax grouping)
                return RepairChain(model, grouping.Expression, cancellation) is { } inner ? grouping.WithExpression(inner) : null;
            if (expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } call) return null;
            if (member.Name.Identifier.ValueText == "WithRole" && ReadReference(model, call, cancellation) is not null)
                return ResolveRoleCall(model, call, member, cancellation)?.Call;
            return RepairChain(model, member.Expression, cancellation) is { } receiver
                ? call.WithExpression(member.WithExpression(receiver)) : null;
        }

        private static ISymbol? BindSpeculatively(SemanticModel model, InvocationExpressionSyntax original, InvocationExpressionSyntax probe) =>
            model.GetSpeculativeSymbolInfo(original.SpanStart, probe, SpeculativeBindingOption.BindAsExpression).Symbol;

        private bool IsFrameworkRole(IMethodSymbol method)
        {
            var extension = method.ReducedFrom ?? method;
            return extension.IsExtensionMethod && extension.Name == "WithRole" &&
                (SymbolEqualityComparer.Default.Equals(extension.ContainingType, buildExtensions) ||
                 SymbolEqualityComparer.Default.Equals(extension.ContainingType, _styleExtensions));
        }

        private INamedTypeSymbol? GetControlType(ITypeSymbol? receiver)
        {
            if (receiver is not INamedTypeSymbol type || _element is null) return null;
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, wrapped))
            {
                if (type.TypeArguments[0] is not INamedTypeSymbol control) return null;
                type = control;
            }
            return DerivesFrom(type, _element) ? type : null;
        }

        private bool CanExpose(ITypeSymbol type) => type switch
        {
            INamedTypeSymbol named => named.TypeKind != TypeKind.Error && !named.IsFileLocal &&
                compilation.IsSymbolAccessibleWithin(named, compilation.Assembly) &&
                (named.ContainingType is null || CanExpose(named.ContainingType)) && named.TypeArguments.All(CanExpose),
            IArrayTypeSymbol array => CanExpose(array.ElementType),
            ITypeParameterSymbol => false,
            _ => true
        };

        public string? Render()
        {
            if (_roles is null) return null;
            var source = new StringBuilder("// <auto-generated/>\n#nullable enable\n\n");
            if (!owner.ContainingNamespace.IsGlobalNamespace)
                source.Append("namespace ").Append(owner.ContainingNamespace.ToDisplayString()).AppendLine(";").AppendLine();
            source.AppendLine("/// <summary>Provides the typed style role tokens inferred from this view's content.</summary>");
            source.Append("internal static class @").AppendLine(_holder).AppendLine("{");
            foreach (var (name, control) in _roles)
            {
                source.AppendLine("    /// <summary>Gets the shared token for this view role.</summary>");
                source.Append("    internal static ").Append(RoleType).Append('<')
                    .Append(control.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append("> @").Append(name)
                    .Append(" { get; } = new(").Append(SymbolDisplay.FormatLiteral(name, quote: true)).AppendLine(");");
            }
            return source.AppendLine("}").ToString();
        }
    }
}
