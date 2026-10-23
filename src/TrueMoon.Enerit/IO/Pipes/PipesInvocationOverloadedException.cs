namespace TrueMoon.Enerit.IO.Pipes;

/// <summary>A local pipes admission queue is full. No invocation payload was serialized or sent.</summary>
public sealed class PipesInvocationOverloadedException : InvocationException
{
    /// <summary>Creates the bounded-admission error. Remote overload uses the same marker in a normal error response.</summary>
    public PipesInvocationOverloadedException() : base("ENERIT_OVERLOADED: pipes invocation capacity exceeded.") { }
}
