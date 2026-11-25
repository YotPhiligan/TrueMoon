namespace TrueMoon.Aether.Rendering;

/// <summary>Neutral physical image admission; no native surface or transport is involved.</summary>
public static class RenderViewport
{
    /// <summary>Maximum physical width of the first rendering profile.</summary>
    public const int MaximumWidth = 1920;
    /// <summary>Maximum physical height of the first rendering profile.</summary>
    public const int MaximumHeight = 1080;
    /// <summary>Validates 0x0 suspension or an active image within the existing profile.</summary>
    /// <param name="width">Physical width.</param><param name="height">Physical height.</param>
    public static void ValidateExtent(int width, int height)
    {
        if (width == 0 && height == 0) return;
        if (width is < 16 or > MaximumWidth || height is < 16 or > MaximumHeight)
            throw new ArgumentOutOfRangeException(nameof(width), "Unsupported physical viewport extent.");
    }
}
