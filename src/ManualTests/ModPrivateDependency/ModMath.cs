namespace TrueMoon.ManualTests.ModPrivateDependency;

public static class ModMath
{
    public static int Next(int value) => checked(value + 1);
}
