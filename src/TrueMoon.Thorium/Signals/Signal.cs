namespace TrueMoon.Thorium.Signals;


public readonly record struct Signal(byte Index, 
    SignalType Type,
    SignalStatus Status,
    Guid Code,
    int PayloadLenght,
    SignalLocation Location,
    SignalLocation ResponseLocation,
    byte ResponseIndex,
    int ResponseLenght);