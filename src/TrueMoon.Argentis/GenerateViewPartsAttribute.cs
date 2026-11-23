namespace TrueMoon.Argentis;

/// <summary>Opts a view into generated extension properties for named controls declared in its constructors.</summary>
/// <remarks>Use constant C# identifiers with <see cref="ElementExtensions.Named{T}(T, string)"/>.
/// Declare parts of nested views in their own construction scope.</remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateViewPartsAttribute : Attribute;
