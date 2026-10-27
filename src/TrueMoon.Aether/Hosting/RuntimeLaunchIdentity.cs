namespace TrueMoon.Aether.Hosting;

/// <summary>Native bootstrap counterpart paths. They are verified against actual binaries, never used as an unverified hash assertion.</summary>
/// <param name="HostAssemblyPath">Absolute parent managed entry assembly path.</param>
/// <param name="RuntimeAssemblyPath">Absolute child managed entry assembly path.</param>
/// <param name="Version">Exact bootstrap identity schema version; mixed versions are rejected.</param>
public sealed record RuntimeLaunchIdentity(string HostAssemblyPath, string RuntimeAssemblyPath, int Version = 1);
