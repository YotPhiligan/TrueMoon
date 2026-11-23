namespace TrueMoon.Argentis;

public class VStack : StackBase
{
    /// <summary>Creates a new VStack with its constructor defaults.</summary>
    /// <returns>A new independent VStack for Fluent configuration.</returns>
    public static VStack Create() => new();

    /// <summary>Creates a vertical stack owning the supplied children in order.</summary>
    public static VStack Create(params Element[] children) => CreateWithChildren(new VStack(), children);

    public VStack()
    {
    }
}
