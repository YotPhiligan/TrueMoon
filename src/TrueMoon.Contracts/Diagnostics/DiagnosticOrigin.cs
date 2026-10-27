namespace TrueMoon.Diagnostics;

/// <summary>Original collector identity retained when a snapshot enters another bounded history.</summary>
/// <param name="ApplicationId">Original collector.</param><param name="Sequence">Original collector-local sequence.</param>
public sealed record DiagnosticOrigin(Guid ApplicationId, long Sequence);
