namespace TrueMoon.Aether.Tooling.Hosting;

/// <summary>Protected target identity supplied by the headless host's composition root.</summary>
/// <param name="HostContractId">Exact fingerprint accepted by the target Runtime.</param>
/// <param name="RuntimeAccess">Explicit concrete Runtime capability for game compilation.</param>
public sealed record ToolingComposition(string HostContractId, bool RuntimeAccess = false);
