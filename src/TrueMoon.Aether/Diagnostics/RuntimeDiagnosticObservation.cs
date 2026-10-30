using TrueMoon.Aether.Gpu;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Diagnostics;

/// <summary>Independent last-observed child state, retained after exit. It is neither live health nor a final durability acknowledgement.</summary>
/// <param name="CapturedUtc">Child capture time.</param><param name="Collector">Numeric Core health; error fields contain only the fixed marker "omitted" when present.</param>
/// <param name="GraphicsDevice">Last actually selected producer device; null before GPU initialization or in a CPU-only run.</param>
/// <param name="OmittedSinks">Sinks omitted by the observation byte/count budget.</param>
public sealed record RuntimeDiagnosticObservation(DateTimeOffset CapturedUtc, DiagnosticHealthSnapshot? Collector,
    GpuDeviceObservation? GraphicsDevice, int OmittedSinks = 0);

/// <summary>Borrowed memory-only observation source. Reading must not perform IO, native queries, flush or wait for sinks.</summary>
public interface IRuntimeDiagnosticObservationSource
{
    /// <summary>Copies independent cached state; no ownership transfers to the reader.</summary>
    RuntimeDiagnosticObservation Read();
}

internal sealed class RuntimeDiagnosticObservationSource(IDiagnosticHealthReader? health, GpuResourceHost gpu) : IRuntimeDiagnosticObservationSource
{
    public RuntimeDiagnosticObservation Read() => new(DateTimeOffset.UtcNow, health?.ReadHealth(), gpu.LastDevice);
}
