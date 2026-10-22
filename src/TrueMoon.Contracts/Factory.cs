namespace TrueMoon;

/// <summary>Adapts a creation callback without taking ownership of its results or dependencies.</summary>
/// <typeparam name="T">Created value.</typeparam>
public sealed class Factory<T> : IFactory<T>
{
    private readonly Func<T> _create;
    /// <summary>Creates a factory adapter.</summary>
    /// <param name="create">Creation callback; exceptions propagate unchanged.</param>
    public Factory(Func<T> create) { ArgumentNullException.ThrowIfNull(create); _create = create; }
    /// <inheritdoc />
    public T Create() => _create();
    /// <inheritdoc />
    public T Create<TData>(TData? data = default)
        => data is null ? Create() : throw new ArgumentException("This factory does not accept creation arguments.", nameof(data));
}

/// <summary>Adapts a typed creation callback. Its caller owns each returned value.</summary>
/// <typeparam name="T">Created value.</typeparam>
/// <typeparam name="TArguments">Creation arguments.</typeparam>
public sealed class Factory<T, TArguments> : IFactory<T, TArguments>
{
    private readonly Func<TArguments, T> _create;
    /// <summary>Creates a typed factory adapter.</summary>
    /// <param name="create">Creation callback; exceptions propagate unchanged.</param>
    public Factory(Func<TArguments, T> create) { ArgumentNullException.ThrowIfNull(create); _create = create; }
    /// <inheritdoc />
    public T Create(TArguments arguments) => _create(arguments);
}
