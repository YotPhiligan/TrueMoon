using System.Text;
using Microsoft.CodeAnalysis;

namespace TrueMoon.Thorium.Generator;

public class SourceHandle
{
    private readonly GenerationContext _context;
    private readonly INamedTypeSymbol _symbol;
    private readonly StringBuilder _sb = new ();

    public INamedTypeSymbol Symbol => _symbol;
    
    public string TypeValueString { get; }
    
    public SourceHandle(GenerationContext context, INamedTypeSymbol symbol, string sourceNamePostfix = "GeneratedImplementation")
    {
        _context = context;
        _symbol = symbol;
        
        TypeValueString = $"{_symbol}".TrimEnd('?');
        
        var name = _symbol?.Name;

        ImplementationClassName = (name.StartsWith("I") ? name.Substring(1) : name) + sourceNamePostfix;
    }

    public string ImplementationClassName { get; private set; }
    public GenerationContext Context => _context;

    public void Complete()
    {
        _sb.AppendLine("}");
        var source = _sb.ToString();
        var hintName = $"{ImplementationClassName.Replace("?",string.Empty).Replace(" ",string.Empty)}.g.cs";
        _context.SourceProductionContext.AddSource(hintName, source);
    }

    public void AppendLine(string? s = default)
    {
        if (s == null)
        {
            _sb.AppendLine();
        }
        else
        {
            _sb.AppendLine(s);
        }
    }
}