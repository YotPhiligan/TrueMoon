using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;

namespace TrueMoon.Diagnostics;

internal static class DiagnosticSnapshot
{
    internal static DiagnosticRecord Create(string source, DiagnosticMessage message, Guid appId,
        long sequence, DiagnosticPipelineOptions options)
    {
        var truncated = false;
        string? Bound(string? value, int limit)
        {
            if (value is null || value.Length <= limit) return value;
            truncated = true;
            // Do not split a UTF16 surrogate pair at the retention boundary.
            var count = limit > 0 && char.IsHighSurrogate(value[limit - 1]) ? limit - 1 : limit;
            return value[..count];
        }
        var properties = new Dictionary<string, string?>(StringComparer.Ordinal);
        var examined = 0;
        if (message.Properties is not null)
            foreach (var pair in message.Properties)
            {
                if (examined++ >= options.MaxProperties) { truncated = true; break; }
                var key = Bound(pair.Key, options.MaxNameChars)!;
                if (!properties.TryAdd(key, Bound(pair.Value, options.MaxPropertyChars))) truncated = true;
            }
        var activity = Activity.Current;
        return new(DateTimeOffset.UtcNow, sequence, appId, Environment.ProcessId,
            Bound(source, options.MaxNameChars)!, message.Level, message.EventId,
            Bound(message.EventName, options.MaxNameChars)!, Bound(message.MessageTemplate, options.MaxMessageChars)!,
            properties.ToFrozenDictionary(StringComparer.Ordinal),
            Bound(message.Error?.GetType().FullName, options.MaxNameChars),
            Bound(message.Error?.Message, options.MaxExceptionChars),
            Bound(message.Error?.StackTrace, options.MaxExceptionChars),
            activity?.TraceId.ToHexString(), activity?.SpanId.ToHexString(),
            Bound(message.Caller, options.MaxNameChars), truncated);
    }

    internal static DiagnosticMessage Legacy(DiagnosticEvent value)
    {
        if (value.Payload is DiagnosticMessage structured) return structured;
        var level = value.EventLevel switch
        {
            DiagnosticEventLevel.Trace => DiagnosticLevel.Trace,
            DiagnosticEventLevel.Exception => DiagnosticLevel.Error,
            _ => DiagnosticLevel.Information
        };
        var text = value.Payload switch
        {
            null => value.Category ?? value.Caller ?? "Trace",
            string s => s,
            Exception => "Exception",
            bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or char
                => Convert.ToString(value.Payload, CultureInfo.InvariantCulture)!,
            _ => "<" + value.Payload.GetType().FullName + ">"
        };
        return new(level, 0, value.Category ?? "Legacy", text, Error: value.Payload as Exception, Caller: value.Caller);
    }

    internal static long Size(DiagnosticRecord value)
    {
        long chars = value.Source.Length + value.EventName.Length + value.MessageTemplate.Length +
            (value.ExceptionType?.Length ?? 0) + (value.ExceptionMessage?.Length ?? 0) +
            (value.ExceptionStackTrace?.Length ?? 0) + (value.Caller?.Length ?? 0) + 64;
        foreach (var pair in value.Properties) chars += pair.Key.Length + (pair.Value?.Length ?? 0) + 32;
        // Conservative payload/accounting estimate; all retained strings are bounded independently.
        return 512 + chars * 2;
    }
}
