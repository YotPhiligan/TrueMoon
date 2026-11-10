using TrueMoon.Configuration;

namespace TrueMoon.Alloy.Hosting;

/// <summary>Standard initial-window schema; native rendering stays an explicit application choice.</summary>
public static class WindowConfigurationSchema
{
    /// <summary>Exact schema used by UseConfiguredPresentation.</summary>
    public static IConfigurationSchema<WindowSettings> Default { get; } = Create("Alloy:Window", new());

    /// <summary>Creates a module-specific window schema with detached defaults and optional pure bounds.</summary>
    /// <param name="section">Logical section.</param><param name="defaults">Initial scalar defaults.</param><param name="validate">Additional module bounds.</param><returns>A detached schema.</returns>
    public static IConfigurationSchema<WindowSettings> Create(string section, WindowSettings defaults, Action<WindowSettings>? validate = null)
    {
        ArgumentNullException.ThrowIfNull(defaults); var frozen = defaults with { };
        return ConfigurationSchemaDefinitions.Create(section,
        () => frozen with { }, value => value with { }, value => { value.Validate(); validate?.Invoke(value); }, fields =>
        {
            fields.Field("GraphicsApi", value => value.GraphicsApi, (value, next) => value.GraphicsApi = next);
            fields.Field("Width", value => value.Width, (value, next) => value.Width = next);
            fields.Field("Height", value => value.Height, (value, next) => value.Height = next);
            fields.Field("Title", value => value.Title, (value, next) => value.Title = next);
            fields.Field("EnableDiagnostics", value => value.EnableDiagnostics, (value, next) => value.EnableDiagnostics = next);
            fields.Field("FrameMode", value => value.FrameMode, (value, next) => value.FrameMode = next);
            fields.Field("FramesPerSecond", value => value.FramesPerSecond, (value, next) => value.FramesPerSecond = next);
            fields.Field("UseDefaultWindowFrame", value => value.UseDefaultWindowFrame, (value, next) => value.UseDefaultWindowFrame = next);
            fields.Field("TitleBarOverContent", value => value.TitleBarOverContent, (value, next) => value.TitleBarOverContent = next);
            fields.Field("UseSystemAccentBorder", value => value.UseSystemAccentBorder, (value, next) => value.UseSystemAccentBorder = next);
        });
    }
}
