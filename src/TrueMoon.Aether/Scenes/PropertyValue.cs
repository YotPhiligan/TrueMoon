namespace TrueMoon.Aether.Scenes;

/// <summary>Supported scalar kinds in the first authoring profile.</summary>
public enum PropertyValueKind
{
    /// <summary>Explicit null, including the default PropertyValue.</summary>
    Null,
    /// <summary>Boolean true or false.</summary>
    Boolean,
    /// <summary>Signed 64-bit integer.</summary>
    Integer,
    /// <summary>Finite double-precision number.</summary>
    Number,
    /// <summary>Bounded UTF-16 string.</summary>
    String
}

/// <summary>Immutable scalar value. Null is distinct from an absent property override.</summary>
public readonly record struct PropertyValue
{
    private readonly bool _boolean;
    private readonly long _integer;
    private readonly double _number;
    private readonly string? _string;

    private PropertyValue(PropertyValueKind kind, bool boolean = false, long integer = 0, double number = 0, string? text = null)
    { Kind = kind; _boolean = boolean; _integer = integer; _number = number; _string = text; }

    /// <summary>Maximum string length in UTF-16 code units for this profile.</summary>
    public const int MaximumStringLength = 4096;
    /// <summary>Gets the exact kind; integer/number conversions are explicit.</summary>
    public PropertyValueKind Kind { get; }
    /// <summary>Gets an explicit null value.</summary>
    public static PropertyValue Null => default;
    /// <summary>Creates a Boolean value.</summary>
    /// <param name="value">Boolean value.</param>
    public static PropertyValue From(bool value) => new(PropertyValueKind.Boolean, boolean: value);
    /// <summary>Creates a signed 64-bit integer value.</summary>
    /// <param name="value">Integer without floating-point coercion.</param>
    public static PropertyValue From(long value) => new(PropertyValueKind.Integer, integer: value);
    /// <summary>Creates a finite double-precision number.</summary>
    /// <param name="value">Finite number without integer coercion.</param>
    public static PropertyValue From(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "A property number must be finite.");
        return new(PropertyValueKind.Number, number: value);
    }
    /// <summary>Creates a bounded, nonnull string value.</summary>
    /// <param name="value">String of at most MaximumStringLength UTF-16 code units.</param>
    public static PropertyValue From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > MaximumStringLength) throw new ArgumentOutOfRangeException(nameof(value));
        return new(PropertyValueKind.String, text: value);
    }
    /// <summary>Reads a Boolean; rejects other kinds.</summary>
    public bool GetBoolean() => Kind == PropertyValueKind.Boolean ? _boolean : throw WrongKind();
    /// <summary>Reads an integer; rejects other kinds.</summary>
    public long GetInteger() => Kind == PropertyValueKind.Integer ? _integer : throw WrongKind();
    /// <summary>Reads a number; rejects other kinds.</summary>
    public double GetNumber() => Kind == PropertyValueKind.Number ? _number : throw WrongKind();
    /// <summary>Reads a string; rejects other kinds.</summary>
    public string GetString() => Kind == PropertyValueKind.String ? _string! : throw WrongKind();
    private InvalidOperationException WrongKind() => new($"Cannot read property kind {Kind} as the requested kind.");
}
