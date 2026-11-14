using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Configuration;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Independent UI startup schemas. Scene/artifact identity and authoring state are explicit inputs.</summary>
public static class EditorUiConfigurationSchemas
{
    /// <summary>Standard Runtime UI window; the optional full Editor retains its separate custom-chrome Editor:Window schema.</summary>
    public static IConfigurationSchema<WindowSettings> Window { get; } = WindowConfigurationSchema.Create("Editor:RuntimeWindow", new()
    {
        Width = 1280, Height = 800, Title = "TrueMoon Aether Runtime", FrameMode = UiFrameMode.Continuous
    }, value => { if (value.Width < 1100 || value.Height < 700) throw new ArgumentException("The standard editor window requires at least 1100x700."); });
}
