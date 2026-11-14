namespace TrueMoon.Aether.Editor.UI;

public interface IEditorStatusBarViewModel : System.ComponentModel.INotifyPropertyChanged
{
    /// <summary>Short primary status.</summary>
    string Summary { get; }

    /// <summary>Semantic status independent of UI colors and icons.</summary>
    EditorStatusKind Kind { get; }

    /// <summary>Scene occurrence count.</summary>
    string EntityText { get; }

    /// <summary>Runtime state, tick and process identity for the details panel.</summary>
    string RuntimeDetails { get; }

    /// <summary>Current game operation details.</summary>
    string OperationDetails { get; }

    /// <summary>Compact preview activity.</summary>
    string PreviewSummary { get; }

    /// <summary>Actual preview revision and pending work.</summary>
    string PreviewDetails { get; }

    /// <summary>Independent source badge; it does not replace the primary runtime/command status.</summary>
    string SourceSummary { get; }

    /// <summary>Last observation, baseline and coverage details.</summary>
    string SourceDetails { get; }

    /// <summary>Source-specific semantic styling.</summary>
    EditorStatusKind SourceKind { get; }

    /// <summary>Whether the current project has a source segment.</summary>
    bool HasSource { get; }

    /// <summary>Complete runtime/document/preview diagnostics.</summary>
    string ErrorDetails { get; }

    /// <summary>Whether diagnostics are available.</summary>
    bool HasError { get; }

    /// <summary>Whether current work can be cancelled.</summary>
    bool CanCancel { get; }

    /// <summary>Existing committed-document preview admission.</summary>
    bool CanPreview { get; }

    /// <summary>Existing preview stop/recovery admission.</summary>
    bool CanStopPreview { get; }

    /// <summary>Cancels the active document/game operation, or drains a pending preview.</summary>
    Task CancelAsync();

    /// <summary>Uses the existing guarded preview command.</summary>
    void ShowPreview();

    /// <summary>Fences and drains the existing preview owner.</summary>
    Task StopPreviewAsync();
}
