using System.Runtime.CompilerServices;

namespace TrueMoon.Diagnostics;

public enum DiagnosticEventLevel
{
    Trace,
    Message,
    Exception,
}

public record DiagnosticEvent(DateTime Time, DiagnosticEventLevel EventLevel, object? Payload = default, string? Category = default, [CallerMemberName]string? Caller = default)
{
    public string Name { get; private set; }

    public void SetName(string name) => Name = name;
    
    public static DiagnosticEvent Create<T>(T payload, string? category = default, [CallerMemberName]string? caller = default) 
        => new (DateTime.Now, DiagnosticEventLevel.Message, payload, category, caller);
    public static DiagnosticEvent Trace(string? category = default, [CallerMemberName]string? caller = default) 
        => new (DateTime.Now, DiagnosticEventLevel.Trace, Category:category, Caller:caller);
    public static DiagnosticEvent Exception(Exception e, string? category = default, [CallerMemberName]string? caller = default)
    {
        var diagnosticEvent = new DiagnosticEvent(DateTime.Now, DiagnosticEventLevel.Exception, e, Category: category, Caller: caller);
        diagnosticEvent.SetName("TrueMoon.Exception");
        return diagnosticEvent;
    }
}