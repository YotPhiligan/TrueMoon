namespace TrueMoon.Alloy.Hosting;

/// <summary>Marks the public constructor that generated view activations must call.
/// Required when a registered view has more than one longest public constructor.</summary>
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class ViewConstructorAttribute : Attribute
{
}
