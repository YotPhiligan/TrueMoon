using TrueMoon.Alloy.Runtime;

namespace TrueMoon.Alloy.Hosting;

/// <summary>Data-only initial window settings. Renderer, fonts, chrome, appearance and owner-thread factories are explicit composition.</summary>
public sealed record WindowSettings
{
    /// <summary>Initial GraphicsApi value; changes require a new window/App.</summary>
    public UiWindowGraphicsApi? GraphicsApi { get; set; } = null;
    /// <summary>Initial Width value; changes require a new window/App.</summary>
    public int Width { get; set; } = 960;
    /// <summary>Initial Height value; changes require a new window/App.</summary>
    public int Height { get; set; } = 540;
    /// <summary>Initial Title value; changes require a new window/App.</summary>
    public string Title { get; set; } = "TrueMoon UI";
    /// <summary>Initial EnableDiagnostics value; changes require a new window/App.</summary>
    public bool EnableDiagnostics { get; set; } = false;
    /// <summary>Initial FrameMode value; changes require a new window/App.</summary>
    public UiFrameMode FrameMode { get; set; } = UiFrameMode.Automatic;
    /// <summary>Initial FramesPerSecond value; changes require a new window/App.</summary>
    public int FramesPerSecond { get; set; } = 60;
    /// <summary>Initial UseDefaultWindowFrame value; changes require a new window/App.</summary>
    public bool UseDefaultWindowFrame { get; set; } = true;
    /// <summary>Initial TitleBarOverContent value; changes require a new window/App.</summary>
    public bool TitleBarOverContent { get; set; } = false;
    /// <summary>Initial UseSystemAccentBorder value; changes require a new window/App.</summary>
    public bool UseSystemAccentBorder { get; set; } = false;
    /// <summary>Copies only scalar settings, preserving borrowed style and factory composition.</summary>
    /// <param name="options">Window options being composed.</param>
    public void ApplyTo(SilkUiWindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.GraphicsApi = GraphicsApi;
        options.Width = Width;
        options.Height = Height;
        options.Title = Title;
        options.EnableDiagnostics = EnableDiagnostics;
        options.FrameMode = FrameMode;
        options.FramesPerSecond = FramesPerSecond;
        options.UseDefaultWindowFrame = UseDefaultWindowFrame;
        options.TitleBarOverContent = TitleBarOverContent;
        options.UseSystemAccentBorder = UseSystemAccentBorder;
    }
    internal void Validate()
    {
        if (Width <= 0 || Height <= 0 || FramesPerSecond is < 1 or > 240 || !Enum.IsDefined(FrameMode) ||
            GraphicsApi is { } api && !Enum.IsDefined(api)) throw new ArgumentException("Invalid window settings.");
        if (string.IsNullOrWhiteSpace(Title) || Title.Any(char.IsControl)) throw new ArgumentException("Invalid window title.");
    }
}
