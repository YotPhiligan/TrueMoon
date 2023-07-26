namespace TrueMoon.Aluminum;

[Flags]
public enum Alignment
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Top = 1 << 2,
    Bottom = 1 << 3,
    Center = 1 << 4,
    Stretch = 1 << 5,
}