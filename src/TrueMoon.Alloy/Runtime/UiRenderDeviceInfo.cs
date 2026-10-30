namespace TrueMoon.Alloy.Runtime;

/// <summary>Detached host evidence from the graphics context actually used by a surface; no native handles.</summary>
/// <param name="Api">Graphics API.</param><param name="Name">Renderer/device name.</param>
/// <param name="Vendor">Vendor name or numeric identifier.</param><param name="Version">API/driver version reported by the host.</param>
public sealed record UiRenderDeviceInfo(string Api, string Name, string Vendor, string Version);

/// <summary>Optional cached rendering observation. Getters must perform no native calls or waits.</summary>
public interface IUiRenderDiagnostics
{
    /// <summary>Device evidence copied during host initialization; null when unavailable. This does not report current GPU health.</summary>
    UiRenderDeviceInfo? DeviceInfo { get; }
}
