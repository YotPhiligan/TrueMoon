namespace AlloyVulkanTest;

internal sealed record PixelDifference(bool Passed, int DifferentPixels, int OutsideTolerancePixels, int EdgeMatchedPixels, int MaximumChannelDifference,
    double MeanChannelDifference, int? FirstX, int? FirstY)
{
    internal static PixelDifference Compare(byte[] expected, byte[] actual, int width, int height, int tolerance, int edgeRadius = 0, int edgeTolerance = 24)
    {
        if (width <= 0 || height <= 0 || expected.Length != checked(width * height * 4) || actual.Length != expected.Length)
            throw new ArgumentException("Equal complete RGBA images and positive dimensions are required.");
        if (tolerance is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (edgeRadius is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(edgeRadius));
        if (edgeTolerance is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(edgeTolerance));
        var different = 0; var outside = 0; var edges = 0; var maximum = 0; long total = 0; int? x = null, y = null;
        for (var offset = 0; offset < expected.Length; offset += 4)
        {
            var pixelMaximum = 0;
            for (var channel = 0; channel < 4; channel++)
            {
                var delta = Math.Abs(expected[offset + channel] - actual[offset + channel]);
                pixelMaximum = Math.Max(pixelMaximum, delta); total += delta;
            }
            maximum = Math.Max(maximum, pixelMaximum);
            if (pixelMaximum != 0) different++;
            if (pixelMaximum <= tolerance) continue;
            if (edgeRadius == 1 && InEnvelope(expected, actual, offset, width, height, tolerance, edgeTolerance)
                && InEnvelope(actual, expected, offset, width, height, tolerance, edgeTolerance)) { edges++; continue; }
            outside++; x ??= offset / 4 % width; y ??= offset / 4 / width;
        }
        return new(outside == 0, different, outside, edges, maximum, total / (double)expected.Length, x, y);
    }

    // Both directions are required: adding/removing an edge must not be accepted merely
    // because the other image has a nearby background pixel. Flat interiors stay strict.
    private static bool InEnvelope(byte[] neighborhood, byte[] sample, int offset, int width, int height, int tolerance, int edgeTolerance)
    {
        var x = offset / 4 % width; var y = offset / 4 / width;
        for (var channel = 0; channel < 4; channel++)
        {
            var minimum = 255; var maximum = 0;
            for (var row = Math.Max(0, y - 1); row <= Math.Min(height - 1, y + 1); row++)
                for (var column = Math.Max(0, x - 1); column <= Math.Min(width - 1, x + 1); column++)
                {
                    var value = neighborhood[(row * width + column) * 4 + channel];
                    minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
                }
            var allowed = maximum - minimum >= 8 ? Math.Max(tolerance, edgeTolerance) : tolerance;
            if (sample[offset + channel] < minimum - allowed || sample[offset + channel] > maximum + allowed) return false;
        }
        return true;
    }

    internal static byte[] Diff(byte[] expected, byte[] actual)
    {
        if (expected.Length != actual.Length || expected.Length % 4 != 0) throw new ArgumentException("Equal RGBA buffers are required.");
        var pixels = new byte[expected.Length];
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            var delta = 0;
            for (var channel = 0; channel < 4; channel++) delta = Math.Max(delta, Math.Abs(expected[offset + channel] - actual[offset + channel]));
            pixels[offset] = pixels[offset + 2] = (byte)Math.Min(255, delta * 16); pixels[offset + 3] = 255;
        }
        return pixels;
    }
}
