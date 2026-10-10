namespace TrueMoon.Alloy;

/// <summary>The confirmed terminal rendering failure category.</summary>
public enum UiRenderingFailureKind
{
    /// <summary>The host graphics device has been lost.</summary>
    DeviceLost,
    /// <summary>The graphics context has been lost or abandoned.</summary>
    ContextLost,
    /// <summary>The backend cannot safely continue this session.</summary>
    BackendFailure
}
/// <summary>A terminal graphics error; ordinary drawing errors remain retryable.</summary>
public sealed class UiRenderingException : Exception
{
    /// <summary>The reporting backend or host.</summary>
    public string Backend { get; }
    /// <summary>The failed operation.</summary>
    public string Operation { get; }
    /// <summary>The confirmed category.</summary>
    public UiRenderingFailureKind Kind { get; }
    /// <summary>Describes the failure while preserving its original cause.</summary>
    /// <param name="backend">Nonempty backend name.</param>
    /// <param name="operation">Nonempty operation.</param>
    /// <param name="kind">Confirmed terminal category.</param>
    /// <param name="cause">Original graphics failure.</param>
    public UiRenderingException(string backend, string operation, UiRenderingFailureKind kind, Exception cause)
        : base($"{backend} {operation}: {kind}.", cause ?? throw new ArgumentNullException(nameof(cause)))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backend); ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Backend = backend; Operation = operation; Kind = kind;
    }
}
