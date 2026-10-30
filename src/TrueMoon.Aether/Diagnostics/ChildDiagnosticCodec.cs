using System.Collections.Frozen;
using System.Text.Json;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Diagnostics;

internal static class ChildDiagnosticCodec
{
    internal const int WireLimit = 72 * 1024;
    internal const int EnvelopeBytes = 1024;
    internal static readonly JsonSerializerOptions Json = new() { MaxDepth = 16 };
    internal static DiagnosticRecord Bound(DiagnosticRecord record)
    {
        var truncated = record.IsTruncated;
        string? Clip(string? text, int limit)
        {
            if (text == null || text.Length <= limit) return text;
            truncated = true; var count = char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit;
            return text[..count];
        }
        var properties = new Dictionary<string, string?>(StringComparer.Ordinal); var inspected = 0;
        foreach (var pair in record.Properties)
        {
            if (inspected++ == 16) { truncated = true; break; }
            if (string.IsNullOrWhiteSpace(pair.Key) || Sensitive(pair.Key)) { truncated = true; continue; }
            if (!properties.TryAdd(Clip(pair.Key, 64)!, Clip(pair.Value, 128))) truncated = true;
        }
        return record with
        {
            Source = Clip(record.Source, 128)!, EventName = Clip(record.EventName, 128)!, MessageTemplate = Clip(record.MessageTemplate, 2048)!,
            ExceptionType = Clip(record.ExceptionType, 128), ExceptionMessage = Clip(record.ExceptionMessage, 2048),
            ExceptionStackTrace = Clip(record.ExceptionStackTrace, 4096), Caller = Clip(record.Caller, 128),
            Properties = properties.ToFrozenDictionary(StringComparer.Ordinal), IsTruncated = truncated
        };
    }
    internal static bool Sensitive(string key) => key.Equals("Nonce", StringComparison.OrdinalIgnoreCase) || key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase);
    internal static DiagnosticRecord[] Decode(DiagnosticDeliveryPacket packet, ChildDiagnosticDeliveryOptions options)
        => DecodeEnvelope(packet, options).Records;

    internal static (DiagnosticRecord[] Records, RuntimeDiagnosticObservation? Observation, bool Omitted) DecodeEnvelope(DiagnosticDeliveryPacket packet, ChildDiagnosticDeliveryOptions options)
    {
        if (packet.Payload == null || packet.Payload.Length > options.MaxBatchBytes || packet.RecordCount < 0 || packet.RecordCount > options.MaxBatchRecords)
            throw new InvalidDataException("Invalid diagnostic packet bounds.");
        using var document = JsonDocument.Parse(packet.Payload, new JsonDocumentOptions { MaxDepth = 16 });
        var body = document.RootElement; RuntimeDiagnosticObservation? observation = null; var omitted = false;
        if (packet.Version == 2)
        {
            if (body.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid diagnostic envelope.");
            var value = body.GetProperty("Observation"); omitted = body.GetProperty("ObservationOmittedByBudget").GetBoolean();
            if (value.ValueKind != JsonValueKind.Null)
            {
                if (omitted || System.Text.Encoding.UTF8.GetByteCount(value.GetRawText()) > RuntimeDiagnosticObservationCodec.MaximumBytes) throw new InvalidDataException("Invalid observation size.");
                observation = RuntimeDiagnosticObservationCodec.Decode(value, packet.ApplicationId);
            }
            body = body.GetProperty("Records");
        }
        else if (packet.Version != 1) throw new InvalidDataException("Invalid diagnostic version.");
        if (body.ValueKind != JsonValueKind.Array || body.GetArrayLength() != packet.RecordCount)
            throw new InvalidDataException("Invalid diagnostic record count.");
        var records = body.Deserialize<DiagnosticRecord[]>(Json) ?? throw new InvalidDataException("Missing diagnostic records.");
        long sequence = 0;
        foreach (var record in records)
        {
            if (record == null || record.Origin != null || record.ApplicationId != packet.ApplicationId || record.ProcessId != packet.ProcessId ||
                record.Sequence <= sequence || !Enum.IsDefined(record.Level) || string.IsNullOrWhiteSpace(record.Source) ||
                record.Source.Length > 128 || record.EventName == null || record.EventName.Length > 128 || record.MessageTemplate == null ||
                record.MessageTemplate.Length > 2048 || (record.ExceptionType?.Length ?? 0) > 128 || (record.ExceptionMessage?.Length ?? 0) > 2048 ||
                (record.ExceptionStackTrace?.Length ?? 0) > 4096 || (record.Caller?.Length ?? 0) > 128 || record.Properties == null ||
                record.Properties.Count > 16 || record.Properties.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 64 ||
                    (pair.Value?.Length ?? 0) > 128 || Sensitive(pair.Key)))
                throw new InvalidDataException("Invalid diagnostic record.");
            sequence = record.Sequence;
        }
        return (records, observation, omitted);
    }
    internal static byte[] Envelope(byte[] records, byte[]? observation, bool omitted)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject(); json.WritePropertyName("Records"); json.WriteRawValue(records);
            json.WritePropertyName("Observation"); if (observation == null) json.WriteNullValue(); else json.WriteRawValue(observation);
            json.WriteBoolean("ObservationOmittedByBudget", omitted); json.WriteEndObject();
        }
        return buffer.ToArray();
    }
}
