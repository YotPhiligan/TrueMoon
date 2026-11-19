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

    /// <summary>Advisory source status of the selected model imports, or empty for scenes without a selection.</summary>
    string ModelImportDetails => "";
    /// <summary>Whether the active document has selected model imports.</summary>
    bool HasModelImports => ModelImportDetails.Length != 0;

    /// <summary>Available model reimport targets in this opening.</summary>
    IReadOnlyList<EditorModelReimportAction> ModelReimports => [];

    /// <summary>Requests reimport for the displayed target.</summary>
    /// <param name="action">Exact model and document selection shown by the view.</param>
    void ReimportModel(EditorModelReimportAction action) { }

    /// <summary>Whether the active project has a model catalog.</summary>
    bool HasModelCatalog => false;
    /// <summary>Whether reading the project model list is currently admitted.</summary>
    bool CanReadModelCatalog => false;
    /// <summary>Availability or empty-state message for the catalog.</summary>
    string ModelCatalogMessage => "";
    /// <summary>Latest project model rows in the context of the active scene.</summary>
    IReadOnlyList<EditorProjectModelRow> ProjectModels => [];
    /// <summary>Reads or refreshes the available project models.</summary>
    void ReadModelCatalog() { }
    /// <summary>Requests a selection change for the displayed model and opening.</summary>
    /// <param name="row">Exact displayed selection target.</param>
    void SelectModel(EditorProjectModelRow row) { }
    /// <summary>Adds the exact selected model generation to the open scene.</summary>
    /// <param name="row">Displayed model and opening.</param>
    void PlaceModel(EditorProjectModelRow row) { }

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
