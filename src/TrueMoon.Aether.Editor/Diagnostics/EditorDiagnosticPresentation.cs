namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>Value-only observation of the presentation used by the owning UI session; no renderer or native handles.</summary>
/// <param name="Backend">Observed rendering surface type or host-provided backend identity.</param>
/// <param name="Width">Pixel width.</param><param name="Height">Pixel height.</param><param name="Scale">Logical-to-pixel scale.</param>
/// <param name="Device">Actual device identity when supplied by the host; null means unavailable.</param>
public sealed record EditorDiagnosticPresentation(string Backend, int Width, int Height, float Scale, string? Device = null);
