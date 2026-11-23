namespace TrueMoon.Argentis;

/// <summary>Opts a UiProperty field into a generated two-way binding method on its control.</summary>
[AttributeUsage(AttributeTargets.Field)]
internal sealed class FluentTwoWayBindingAttribute : Attribute;
