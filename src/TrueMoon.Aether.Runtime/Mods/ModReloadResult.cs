namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>Neutral replacement result. A completed commit is never rolled back because retirement later fails.</summary>
/// <param name="Committed">Whether scheduler/generation ownership switched at the serialized tick boundary.</param>
/// <param name="Generation">Current published generation, zero before initial load.</param>
/// <param name="Diagnostic">Preparation/rollback outcome, without plugin exceptions.</param>
/// <param name="Cleanup">Separate failed-candidate/retirement cleanup outcome, safe to retry.</param>
/// <param name="UnloadTicket">Weak observation of the retired or rejected context, if one was allocated.</param>
public sealed record ModReloadResult(bool Committed, long Generation, ModDiagnostic Diagnostic, ModDiagnostic Cleanup, ModUnloadTicket? UnloadTicket);
