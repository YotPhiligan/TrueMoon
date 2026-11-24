namespace TrueMoon.Aether.Abstractions.Sessions;

/// <summary>A numeric observation with a unit defined and versioned by the game.</summary>
/// <param name="Value">Finite numeric value at the completed tick.</param>
/// <param name="Unit">Nonblank, case-sensitive unit label.</param>
public readonly record struct GameScenarioNumber(double Value, string Unit);
