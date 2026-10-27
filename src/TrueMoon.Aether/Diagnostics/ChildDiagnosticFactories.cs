using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO.Pipes;

namespace TrueMoon.Aether.Diagnostics;

internal static class ChildDiagnosticFactories
{
    internal static PipesTransportOptions Limits(ChildDiagnosticDeliveryOptions options) => new()
    {
        MaxPendingRequests = 1, MaxQueuedRequests = 0, MaxConcurrentRequests = 1,
        MaxBufferedRequestBytes = ChildDiagnosticCodec.WireLimit, MaxConnections = 1,
        RequestTimeout = options.RequestTimeout, MetricsEnabled = false
    };
}

internal sealed class ChildDiagnosticConfiguration(ChildDiagnosticDeliveryOptions options)
{
    private readonly ChildDiagnosticDeliveryOptions _options = options.Snapshot();
    internal ChildDiagnosticDeliveryOptions Copy() => _options.Snapshot();
}

internal sealed class ChildDiagnosticReceiverFactory(IDiagnosticRecordImporter? importer,
    IFactory<PipesInvocationServer<IChildDiagnosticDelivery>, PipesInvocationServerArguments<IChildDiagnosticDelivery>> servers)
    : IFactory<ChildDiagnosticReceiver, ChildDiagnosticReceiverArguments>
{
    public ChildDiagnosticReceiver Create(ChildDiagnosticReceiverArguments arguments)
        => new(arguments.Launch, arguments.ProcessId, importer, servers);
}

internal sealed class ChildDiagnosticForwarderFactory(IDiagnosticRecordReader? reader,
    IFactory<PipesInvocationClient<IChildDiagnosticDelivery>, string> clients)
    : IFactory<ChildDiagnosticForwarder, SessionLaunch>
{
    public ChildDiagnosticForwarder Create(SessionLaunch arguments) => new(arguments, reader, clients);
}
