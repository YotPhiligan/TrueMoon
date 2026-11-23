namespace TrueMoon.Argentis;

/// <summary>Opts in an event whose control owns and clears its handlers on disposal.</summary>
[AttributeUsage(AttributeTargets.Event)]
internal sealed class FluentEventAttribute : Attribute
{
    /// <param name="receiverName">Receiver parameter name in the generated extension.</param>
    public FluentEventAttribute(string receiverName = "element") { }
}
