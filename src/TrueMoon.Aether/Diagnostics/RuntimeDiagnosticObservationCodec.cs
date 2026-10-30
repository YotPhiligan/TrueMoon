using System.Text.Json;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Diagnostics;

internal static class RuntimeDiagnosticObservationCodec
{
    internal const int MaximumBytes = 8192;
    internal const int MaximumSinks = 16;
    internal static byte[]? Encode(RuntimeDiagnosticObservation value, int maximumBytes)
    {
        var health = value.Collector;
        var sinks = health?.Sinks.Take(MaximumSinks).Select(sink => sink with { Name = Name(sink.Name), LastError = Marker(sink.LastError) }).ToArray() ?? [];
        var omitted = value.OmittedSinks + Math.Max(0, (health?.Sinks.Count ?? 0) - sinks.Length);
        if (health != null) health = health with
        {
            Sinks = Array.AsReadOnly(sinks),
            Emergency = health.Emergency is { } emergency ? emergency with { LastError = Marker(emergency.LastError) } : null,
            Retention = health.Retention is { } retention ? retention with { LastError = Marker(retention.LastError) } : null
        };
        value = value with { Collector = health, OmittedSinks = omitted,
            GraphicsDevice = value.GraphicsDevice is { } device ? device with { Backend = Name(device.Backend), Name = Text(device.Name, 256) } : null };
        while (true)
        {
            Validate(value, health?.ApplicationId);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, ChildDiagnosticCodec.Json);
            if (bytes.Length <= Math.Min(maximumBytes, MaximumBytes)) return bytes;
            if (value.Collector is not { Sinks.Count: > 0 } collector) return null;
            value = value with { Collector = collector with { Sinks = Array.AsReadOnly(collector.Sinks.Take(collector.Sinks.Count - 1).ToArray()) }, OmittedSinks = value.OmittedSinks + 1 };
        }
    }
    internal static RuntimeDiagnosticObservation Decode(JsonElement element, Guid applicationId)
    {
        var value = element.Deserialize<RuntimeDiagnosticObservation>(ChildDiagnosticCodec.Json) ?? throw new InvalidDataException("Missing runtime observation.");
        Validate(value, applicationId);
        // Detach collections from mutable deserialization buffers before publishing the cached observation.
        return value with { Collector = value.Collector is { } health ? health with { Sinks = Array.AsReadOnly(health.Sinks.ToArray()) } : null };
    }
    private static void Validate(RuntimeDiagnosticObservation value, Guid? applicationId)
    {
        if (value.CapturedUtc <= DateTimeOffset.UnixEpoch || value.OmittedSinks < 0) throw new InvalidDataException("Invalid observation metadata.");
        if (value.GraphicsDevice is { } device && (!Identifier(device.Backend) || string.IsNullOrWhiteSpace(device.Name) || device.Name.Length > 256)) throw new InvalidDataException("Invalid graphics observation.");
        if (value.Collector is not { } health) return;
        if (health.ApplicationId == Guid.Empty || health.ApplicationId != applicationId || health.LatestSequence < 0 || health.RetainedRecords < 0 || health.RetainedBytes < 0 ||
            health.MemoryEvicted < 0 || health.CaptureFailures < 0 || health.LateWrites < 0 || health.Sinks == null || health.Sinks.Count > MaximumSinks)
            throw new InvalidDataException("Invalid collector observation.");
        foreach (var sink in health.Sinks)
            if (sink == null || !Identifier(sink.Name) || sink.Accepted < 0 || sink.Written < 0 || sink.Dropped < 0 || sink.Failures < 0 || sink.Queued < 0 || sink.Bytes < 0 || !ValidMarker(sink.LastError))
                throw new InvalidDataException("Invalid sink observation.");
        if (health.Emergency is { } emergency && (emergency.Accepted < 0 || emergency.Written < 0 || emergency.Dropped < 0 || emergency.Suppressed < 0 || emergency.Failures < 0 || emergency.Queued < 0 || !ValidMarker(emergency.LastError)))
            throw new InvalidDataException("Invalid emergency observation.");
        if (health.Retention is { } retention && (retention.RetainedRuns < 0 || retention.RetainedBytes < 0 || retention.ActiveRuns < 0 || retention.SkippedDirectories < 0 || retention.DeletedRuns < 0 || retention.Failures < 0 || !ValidMarker(retention.LastError)))
            throw new InvalidDataException("Invalid retention observation.");
    }
    private static bool ValidMarker(string? value) => value is null or "omitted";
    private static string? Marker(string? value) => value == null ? null : "omitted";
    private static string Name(string value) => new(value.Take(64).Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray());
    private static bool Identifier(string? value) => value is { Length: > 0 and <= 64 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
    private static string Text(string value, int maximum) => new(value.Take(maximum).Select(c => char.IsControl(c) ? ' ' : c).ToArray());
}
