namespace TrueMoon.Thorium.Signals;

public enum SignalType : byte
{
    None = 0,
    Notification = 1,
    Invocation = 2,
    Request = 3
}

public enum SignalLocation : byte
{
    Small = 0,
    Large = 1,
}