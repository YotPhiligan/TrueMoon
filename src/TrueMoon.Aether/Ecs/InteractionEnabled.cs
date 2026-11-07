namespace TrueMoon.Aether.Ecs;

/// <summary>Runtime value of the existing built-in interaction eligibility flag. Gameplay behavior is separate.</summary>
/// <param name="Enabled">Whether the occurrence is eligible for interaction.</param>
public readonly record struct InteractionEnabled(bool Enabled);
