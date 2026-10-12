namespace Direct.Consumer;

/// <summary>Standalone graph cleanup evidence.</summary>
public sealed class Evidence
{
    internal int SharedDisposals, TransientDisposals;
    internal readonly Borrowed Borrowed = new();
}
/// <summary>First Composite alias.</summary>
public interface IFirst;
/// <summary>Second Composite alias.</summary>
public interface ISecond;
/// <summary>Generated shared singleton.</summary>
/// <param name="state">Cleanup evidence.</param>
public sealed class Shared(Evidence state) : IFirst, ISecond, IDisposable
{
    /// <inheritdoc />
    public void Dispose() => state.SharedDisposals++;
}
/// <summary>Open generic service contract.</summary>
public interface IValue<T>
{
    /// <summary>Resolved generic argument name.</summary>
    string Name { get; }
}
/// <summary>Generated open generic implementation.</summary>
public sealed class Value<T> : IValue<T>
{
    /// <inheritdoc />
    public string Name => typeof(T).Name;
}
/// <summary>Owned generated transient.</summary>
/// <param name="state">Cleanup evidence.</param>
public sealed class Transient(Evidence state) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => state.TransientDisposals++;
}
/// <summary>Caller-owned registration.</summary>
public sealed class Borrowed : IDisposable
{
    internal int Disposals;
    /// <inheritdoc />
    public void Dispose() => Disposals++;
}
internal static class Check
{
    internal static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
