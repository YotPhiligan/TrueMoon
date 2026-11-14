namespace TrueMoon.Kul;

/// <summary>A native audio operation failed. No successful playback is implied.</summary>
public sealed class AudioException : Exception
{
    /// <summary>Creates an error retaining the native result code.</summary>
    /// <param name="operation">Failed operation.</param><param name="nativeResult">MiniAudioEx result code.</param>
    public AudioException(string operation, int nativeResult) : base($"{operation}: native audio result {nativeResult}.")
        => NativeResult = nativeResult;
    /// <summary>Original MiniAudioEx result.</summary>
    public int NativeResult { get; }
}
