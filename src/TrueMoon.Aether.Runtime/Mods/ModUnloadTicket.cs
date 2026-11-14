namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>Non-owning unload observation. Unload is cooperative; only collection of the weak target confirms completion.</summary>
public sealed class ModUnloadTicket
{
    private readonly WeakReference _context;
    internal ModUnloadTicket(object context) => _context = new(context, true);
    /// <summary>Whether a collectible context is still reachable. Observe outside loader/module stack frames.</summary>
    public bool IsAlive => _context.IsAlive;
    /// <summary>Whether cleanup completed and AssemblyLoadContext.Unload was requested.</summary>
    public bool UnloadRequested { get; internal set; }
}

/// <summary>Neutral synchronous operation failure; never contains a plugin exception.</summary>
public sealed class ModOperationException : Exception
{
    internal ModOperationException(ModDiagnostic diagnostic) : base(diagnostic.Message) => Diagnostic = diagnostic;
    /// <summary>Stable operation outcome.</summary>
    public ModDiagnostic Diagnostic { get; }
}
