namespace TrueMoon.Aluminum;

public static class AlignmentExtensions
{
    public static bool HasFlagFast(this Alignment value, Alignment flag) => (value & flag) != 0;
}