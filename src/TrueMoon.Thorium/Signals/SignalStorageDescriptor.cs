namespace TrueMoon.Thorium.Signals;

public readonly record struct SignalStorageDescriptor(byte SmallSignals, int SmallSignalSize, byte LargeSignals, int LargeSignalSize);