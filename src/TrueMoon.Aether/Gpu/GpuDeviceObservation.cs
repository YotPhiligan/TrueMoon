namespace TrueMoon.Aether.Gpu;

/// <summary>Value-only evidence from the device actually selected by a producer; no handles or adapter UUIDs.</summary>
/// <param name="Backend">Graphics API.</param><param name="Name">Bounded device name.</param>
/// <param name="ApiVersion">Native API version.</param><param name="DriverVersion">Native driver version.</param>
/// <param name="VendorId">Native vendor ID.</param><param name="DeviceId">Native device ID.</param>
public sealed record GpuDeviceObservation(string Backend, string Name, uint ApiVersion, uint DriverVersion, uint VendorId, uint DeviceId);
