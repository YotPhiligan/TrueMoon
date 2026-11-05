namespace TrueMoon.Argentis;

/// <summary>Semantic editor accents. Interaction and validation states take precedence over the idle outline.</summary>
public enum EditorColorRole
{
    /// <summary>Ordinary neutral control.</summary>
    Neutral,
    /// <summary>Scalar numeric value.</summary>
    Number,
    /// <summary>Text value.</summary>
    Text,
    /// <summary>Boolean value.</summary>
    Boolean,
    /// <summary>Reference to another object or resource.</summary>
    Reference,
    /// <summary>First spatial axis.</summary>
    AxisX,
    /// <summary>Second spatial axis.</summary>
    AxisY,
    /// <summary>Third spatial axis.</summary>
    AxisZ
}
