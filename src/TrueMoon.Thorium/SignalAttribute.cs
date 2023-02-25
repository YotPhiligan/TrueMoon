namespace TrueMoon.Thorium;

[AttributeUsage(AttributeTargets.Class)]
public class SignalAttribute : Attribute
{
    public string Code { get; set; }
}