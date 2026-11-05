using System.Globalization;

namespace TrueMoon.Argentis;

internal static class NumericAdjustment
{
    internal static bool TryApply(string original, NumericValueKind kind, double step, long ticks, double minimum, double maximum, out string value)
    {
        value = original;
        if (!double.IsFinite(step) || step <= 0 || minimum > maximum) return false;
        if (kind == NumericValueKind.Integer)
        {
            if (!long.TryParse(original, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return false;
            try
            {
                var delta = checked((decimal)Math.Max(1, Math.Round(step, MidpointRounding.AwayFromZero)) * ticks);
                var candidate = checked(integer + delta);
                const double limit = 9223372036854775808d;
                if (candidate < long.MinValue || candidate > long.MaxValue || minimum >= limit || maximum < -limit ||
                    minimum >= -limit && candidate < (long)Math.Ceiling(minimum) ||
                    maximum < limit && candidate > (long)Math.Floor(maximum)) return false;
                value = ((long)candidate).ToString(CultureInfo.InvariantCulture); return true;
            }
            catch (OverflowException) { return false; }
        }
        if (!double.TryParse(original, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) return false;
        var result = number + step * ticks;
        // Prefer exact decimal stepping when both operands round-trip; retain the full Double range otherwise.
        if (decimal.TryParse(original, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalNumber) && (double)decimalNumber == number &&
            decimal.TryParse(step.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalStep) && (double)decimalStep == step)
        {
            try { result = (double)checked(decimalNumber + decimalStep * ticks); }
            catch (OverflowException) { }
        }
        if (!double.IsFinite(result) || result < minimum || result > maximum) return false;
        value = result.ToString("R", CultureInfo.InvariantCulture); return true;
    }
}
