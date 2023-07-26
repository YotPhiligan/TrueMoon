namespace TrueMoon.Thorium.IO.Signals;

public enum SignalStatus : byte
{
    None = 0,
    ReadyToProcess = 1,
    Processing = 2,
    Processed = 3,
    Fail = 4,
    Preparing = 5,
    ResponsePreparing = 6,
}