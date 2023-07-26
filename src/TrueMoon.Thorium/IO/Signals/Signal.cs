namespace TrueMoon.Thorium.IO.Signals;


public readonly record struct Signal(byte Index, 
    SignalType Type,
    SignalStatus Status,
    byte Code,
    Guid Guid,
    int PayloadLenght,
    SignalLocation Location,
    SignalLocation ResponseLocation,
    byte ResponseIndex,
    int ResponseLenght);