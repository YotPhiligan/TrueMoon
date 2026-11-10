using TrueMoon.Configuration;

namespace TrueMoon.Enerit.IO.Pipes;

/// <summary>Standard startup schema for Enerit:Pipes; resources and operation arguments remain separate.</summary>
public static class PipesConfigurationSchema
{
    /// <summary>The exact schema instance used by configured App attachment.</summary>
    public static IConfigurationSchema<PipesTransportOptions> Default { get; } = ConfigurationSchemaDefinitions.Create("Enerit:Pipes", () => new PipesTransportOptions(),
        value => new PipesTransportOptions { MaxPendingRequests = value.MaxPendingRequests, MaxQueuedRequests = value.MaxQueuedRequests, MaxConcurrentRequests = value.MaxConcurrentRequests, MaxBufferedRequestBytes = value.MaxBufferedRequestBytes, MaxConnections = value.MaxConnections, RequestTimeout = value.RequestTimeout, PropagateTraceContext = value.PropagateTraceContext, CaptureRequestEvents = value.CaptureRequestEvents, MetricsEnabled = value.MetricsEnabled }, value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("MaxPendingRequests", value => value.MaxPendingRequests, (value, next) => value.MaxPendingRequests = next);
            fields.Field("MaxQueuedRequests", value => value.MaxQueuedRequests, (value, next) => value.MaxQueuedRequests = next);
            fields.Field("MaxConcurrentRequests", value => value.MaxConcurrentRequests, (value, next) => value.MaxConcurrentRequests = next);
            fields.Field("MaxBufferedRequestBytes", value => value.MaxBufferedRequestBytes, (value, next) => value.MaxBufferedRequestBytes = next);
            fields.Field("MaxConnections", value => value.MaxConnections, (value, next) => value.MaxConnections = next);
            fields.Field("RequestTimeout", value => value.RequestTimeout, (value, next) => value.RequestTimeout = next);
            fields.Field("PropagateTraceContext", value => value.PropagateTraceContext, (value, next) => value.PropagateTraceContext = next);
            fields.Field("CaptureRequestEvents", value => value.CaptureRequestEvents, (value, next) => value.CaptureRequestEvents = next);
            fields.Field("MetricsEnabled", value => value.MetricsEnabled, (value, next) => value.MetricsEnabled = next);
        });
}
