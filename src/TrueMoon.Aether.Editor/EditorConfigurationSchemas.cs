using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Configuration;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;

namespace TrueMoon.Aether.Editor;

/// <summary>Initial editor data schemas. Build/Play captures, projects/documents and UI state are not providers.</summary>
public static class EditorConfigurationSchemas
{
    /// <summary>Scalar standard-window defaults; fonts, chrome and root factories stay explicit.</summary>
    public static IConfigurationSchema<WindowSettings> Window { get; } = WindowConfigurationSchema.Create("Editor:Window", new()
    {
        Width = 1280, Height = 800, Title = "TrueMoon Aether Editor", FrameMode = UiFrameMode.Continuous,
        TitleBarOverContent = true, UseSystemAccentBorder = true
    }, value => { if (value.Width < 1100 || value.Height < 700) throw new ArgumentException("The standard editor window requires at least 1100x700."); });
    /// <summary>Initial project and bounded source tracking policy.</summary>
    public static IConfigurationSchema<AetherEditorOptions> Options { get; } = ConfigurationSchemaDefinitions.Create("Editor", () => new AetherEditorOptions(),
        value => value.Snapshot(), value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("ProjectPath", value => value.ProjectPath, (value, next) => value.ProjectPath = next);
            fields.Field("SourceTracking:Enabled", value => value.SourceTracking.Enabled, (value, next) => value.SourceTracking.Enabled = next);
            fields.Field("SourceTracking:UseFileSystemNotifications", value => value.SourceTracking.UseFileSystemNotifications, (value, next) => value.SourceTracking.UseFileSystemNotifications = next);
            fields.Field("SourceTracking:Debounce", value => value.SourceTracking.Debounce, (value, next) => value.SourceTracking.Debounce = next);
            fields.Field("SourceTracking:MaximumDebounce", value => value.SourceTracking.MaximumDebounce, (value, next) => value.SourceTracking.MaximumDebounce = next);
            fields.Field("SourceTracking:PollInterval", value => value.SourceTracking.PollInterval, (value, next) => value.SourceTracking.PollInterval = next);
            fields.Field("SourceTracking:ScanBudget", value => value.SourceTracking.ScanBudget, (value, next) => value.SourceTracking.ScanBudget = next);
            fields.Field("SourceTracking:MaximumWatchers", value => value.SourceTracking.MaximumWatchers, (value, next) => value.SourceTracking.MaximumWatchers = next);
        });
    /// <summary>Presentation collector budgets, independent of the Core collector.</summary>
    public static IConfigurationSchema<EditorDiagnosticSettings> Diagnostics { get; } = ConfigurationSchemaDefinitions.Create("Editor:Diagnostics",
        () => new EditorDiagnosticSettings(), value => value with { }, value => { _ = value.ToOptions().Snapshot(); }, fields =>
        {
            fields.Field("MaximumRecords", value => value.MaximumRecords, (value, next) => value.MaximumRecords = next);
            fields.Field("MaximumRetainedBytes", value => value.MaximumRetainedBytes, (value, next) => value.MaximumRetainedBytes = next);
            fields.Field("MaximumRecordCharacters", value => value.MaximumRecordCharacters, (value, next) => value.MaximumRecordCharacters = next);
            fields.Field("RecordsPerRefresh", value => value.RecordsPerRefresh, (value, next) => value.RecordsPerRefresh = next);
            fields.Field("RefreshInterval", value => value.RefreshInterval, (value, next) => value.RefreshInterval = next);
            fields.Field("MaximumProcesses", value => value.MaximumProcesses, (value, next) => value.MaximumProcesses = next);
            fields.Field("MaximumRawCharacters", value => value.MaximumRawCharacters, (value, next) => value.MaximumRawCharacters = next);
        });
    /// <summary>Explicit source editor command. Select only when navigation is enabled.</summary>
    public static IConfigurationSchema<SourceEditorOptions> SourceEditor { get; } = ConfigurationSchemaDefinitions.Create("Editor:SourceEditor",
        () => new SourceEditorOptions(), value => new() { ExecutablePath = value.ExecutablePath, Arguments = value.Arguments.ToArray() },
        value => { _ = value.Snapshot(); }, fields =>
        {
            fields.Field("ExecutablePath", value => value.ExecutablePath, (value, next) => value.ExecutablePath = next, new(Required: true));
            fields.Field("Arguments", value => value.Arguments, (value, next) => value.Arguments = next);
        });
}
