namespace TrueMoon;

/// <summary>Creates values whose lifetime is defined by the factory and its caller.</summary>
/// <typeparam name="T">Created value.</typeparam>
public interface IFactory<out T>
{
    /// <summary>Creates a value without runtime arguments.</summary>
    T? Create();
    /// <summary>Creates a value with runtime arguments supported by this factory.</summary>
    /// <typeparam name="TData">Argument type.</typeparam>
    /// <param name="data">Creation arguments.</param>
    T? Create<TData>(TData? data = default);
}

/// <summary>Creates caller-owned values using an explicit argument contract.</summary>
/// <typeparam name="T">Created value.</typeparam>
/// <typeparam name="TArguments">Creation arguments.</typeparam>
public interface IFactory<out T, in TArguments> : IFactory<T>
{
    /// <summary>Creates a value. The result is not automatically tracked by DI.</summary>
    /// <param name="arguments">Arguments for this creation.</param>
    T Create(TArguments arguments);

    T? IFactory<T>.Create() => throw new InvalidOperationException("This factory requires creation arguments.");

    T? IFactory<T>.Create<TData>(TData? data) where TData : default
    {
        if (data is TArguments arguments) return Create(arguments);
        if (data is null && default(TArguments) is null) return Create(default!);
        throw new ArgumentException($"Expected creation arguments of type {typeof(TArguments)}.", nameof(data));
    }
}
