namespace TrueMoon.Argentis.Windows;

/// <summary>Observed window presentation, supplied by a host adapter; no native handles are retained.</summary>
/// <param name="Active">Whether the native window is focused.</param><param name="Maximized">Whether it is maximized.</param>
/// <param name="CanMinimize">Whether minimize is supported.</param><param name="CanMaximize">Whether maximize/restore is supported.</param>
/// <param name="CanResize">Whether native sizing is supported in the current state.</param>
public sealed record WindowFrameState(bool Active, bool Maximized, bool CanMinimize, bool CanMaximize, bool CanResize);