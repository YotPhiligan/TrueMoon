namespace TrueMoon.Argentis;

public class HStack : StackBase
{
    /// <summary>Creates a new HStack with its constructor defaults.</summary>
    /// <returns>A new independent HStack for Fluent configuration.</returns>
    public static HStack Create() => new();

    /// <summary>Creates a horizontal stack owning the supplied children in order.</summary>
    public static HStack Create(params Element[] children) => CreateWithChildren(new HStack(), children);

    protected override bool IsVertical => false;
}
