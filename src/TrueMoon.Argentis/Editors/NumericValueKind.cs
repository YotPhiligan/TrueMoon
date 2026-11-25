namespace TrueMoon.Argentis.Editors;

/// <summary>Arithmetic used by an explicitly enabled numeric adjustment.</summary>
public enum NumericValueKind
{
    /// <summary>Finite invariant-culture floating point values.</summary>
    Real,
    /// <summary>Checked signed Int64 arithmetic without a floating point round trip.</summary>
    Integer
}