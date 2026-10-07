namespace TrueMoon.Argentis;

public class HStack : StackBase
{
    /// <summary>Creates a new HStack with its constructor defaults.</summary>
    /// <returns>A new independent HStack for Fluent configuration.</returns>
    public static HStack Create() => new();

    protected override bool IsVertical => false;
}
