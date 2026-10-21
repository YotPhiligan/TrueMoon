namespace UnitsPackageConsumer;

// Compile-time package analyzer evidence; the probe still obtains adapters through App services.
internal static class AnalyzerEvidence
{
    internal static Type Client => typeof(TrueMoon.Generated.UnitsPackageConsumer.ProbeServiceGeneratedImplementation);
    internal static Type Handler => typeof(TrueMoon.Generated.UnitsPackageConsumer.ProbeServiceInvocationServerHandler);
}
