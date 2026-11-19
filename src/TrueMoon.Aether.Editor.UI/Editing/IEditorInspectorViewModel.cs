using TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.Aether.Editor.UI.Editing;

public interface IEditorInspectorViewModel : System.ComponentModel.INotifyPropertyChanged
{
    bool CanAdjustNumeric(InspectorProperty property);
    bool IsNumericGestureCurrent(InspectorProperty property, long id);
    long BeginNumericGesture(InspectorProperty property);
    void FinishNumericGesture(TrueMoon.Argentis.NumericGestureResult result, long id);
    /// <summary>Opt-in proportional scale editing. Enabling it does not change any field or commit data.</summary>
    bool ScaleLinked { get; set; }

    /// <summary>Whether the selected Transform's link preference can be changed.</summary>
    bool CanLinkScale { get; }

    /// <summary>Whether Position has local values or drafts that can be reset.</summary>
    bool CanResetPosition { get; }

    /// <summary>Whether Rotation has local values or drafts that can be reset.</summary>
    bool CanResetRotation { get; }

    /// <summary>Whether Scale has local values or drafts that can be reset.</summary>
    bool CanResetScale { get; }

    /// <summary>Transient inspector angle display preference. Invalid rotation text, active gestures,
    /// read-only/busy or conflicting state reject changes without altering drafts.</summary>
    bool AnglesInDegrees { get; set; }

    /// <summary>Whether all displayed rotation text can safely change units now, including pending resets.</summary>
    bool CanChangeAngleUnits { get; }

    /// <summary>The compact rotation heading with current display units.</summary>
    string RotationLabel { get; }

    /// <summary>Keyboard and pointer step in current display units.</summary>
    double RotationStep { get; }

    /// <summary>Positive numeric adjustment limit in display units; canonical Float validation remains authoritative.</summary>
    double RotationMaximum { get; }

    /// <summary>Negative numeric adjustment limit in display units.</summary>
    double RotationMinimum { get; }

    /// <summary>Gets the nine stable numeric fields for legacy Transform form consumers. Dynamic UI uses Components.</summary>
    IReadOnlyList<TransformField> Fields { get; }

    /// <summary>Gets whether the selected occurrence can be edited.</summary>
    bool CanEdit { get; }

    /// <summary>Gets valid, changed, nonconflicting draft availability.</summary>
    bool CanApply { get; }

    /// <summary>Gets whether draft cancellation/reload is available.</summary>
    bool CanCancel { get; }

    /// <summary>Gets engine Undo availability; uncommitted drafts must first be applied/cancelled.</summary>
    bool CanUndo { get; }

    /// <summary>Gets engine Redo availability.</summary>
    bool CanRedo { get; }

    /// <summary>Gets explicit preview start/retry availability.</summary>
    bool CanPreview { get; }

    /// <summary>Gets preview stop availability.</summary>
    bool CanStopPreview { get; }

    /// <summary>Gets whether text differs from the captured baseline.</summary>
    bool HasDraft { get; }

    /// <summary>Gets field-independent mutation/conflict diagnostics.</summary>
    string ErrorText { get; }

    /// <summary>Gets whether a complete mutation/conflict diagnostic is available.</summary>
    bool HasError { get; }

    /// <summary>Complete copyable mutation and field diagnostics, including messages clipped in a narrow numeric column.</summary>
    string DiagnosticsText { get; }

    /// <summary>Whether the inspector should expose its full diagnostics panel.</summary>
    bool HasDiagnostics { get; }

    /// <summary>Gets actual presented and requested revision information.</summary>
    string PreviewText { get; }

    /// <summary>Gets only effective components of the selected authoring entity.</summary>
    IReadOnlyList<InspectorComponent> Components { get; }

    /// <summary>Detached choices for the selected MeshRenderer, when present.</summary>
    InspectorRenderAssetSelection? RenderAssets => null;

    /// <summary>Stages an asset choice in the existing inspector draft.</summary>
    /// <param name="selection">Exact displayed renderer choices.</param>
    /// <param name="property">Mesh or material field being edited.</param>
    /// <param name="assetId">Persistent asset identity from the displayed choices.</param>
    void SelectRenderAsset(InspectorRenderAssetSelection selection, InspectorProperty property, Guid assetId) { }

    /// <summary>Stages the current mesh's imported material in the existing inspector draft.</summary>
    /// <param name="selection">Exact displayed renderer selection.</param>
    void RestoreMeshMaterial(InspectorRenderAssetSelection selection) { }

    /// <summary>Gets exact schemas and current dependency/multiplicity admission for addition.</summary>
    IReadOnlyList<InspectorComponentType> AddableTypes { get; }

    /// <summary>Gets hidden inherited occurrences, preserving their identities.</summary>
    IReadOnlyList<InspectorRemovedComponent> RemovedComponents { get; }

    /// <summary>Gets whether the selection has an authoring representation.</summary>
    bool HasSelection { get; }

    /// <summary>Gets whether empty-selection guidance should be shown.</summary>
    bool NoSelection { get; }

    /// <summary>Gets a selection or empty-document description.</summary>
    string SelectionText { get; }

    /// <summary>Gets editing admission for any component, including entities without Transform.</summary>
    bool CanEditProperties { get; }

    /// <summary>Gets presence editing admission; complete or cancel property drafts first.</summary>
    bool CanChangeComponents { get; }

    /// <summary>Copies committed state on the UI thread. Selection changes discard the previous selection's draft;
    /// external revision changes preserve a draft and require explicit cancellation.</summary>
    /// <param name="selectedId">Stable entity GUID, or null.</param>
    void Refresh(string? selectedId);

    /// <summary>Commits changed properties/resets as one shared authoring transaction.</summary>
    void Apply();

    /// <summary>Stages an inheritance/schema reset in the current property draft.</summary>
    /// <param name="property">Current retained field model.</param>
    void ResetProperty(InspectorProperty property);

    /// <summary>Stages inheritance/schema resets for a whole Transform group without committing it.
    /// Clean inherited fields remain untouched. Numeric gestures must finish before resetting.</summary>
    /// <param name="group">The group in the selected built-in Transform.</param>
    void ResetTransformGroup(TransformGroup group);

    /// <summary>Adds a new component occurrence using schema defaults.</summary>
    /// <param name="type">Exact type.</param><param name="version">Exact version.</param>
    void AddComponent(ComponentTypeId type, int version);

    /// <summary>Removes a local occurrence or masks an inherited occurrence.</summary>
    /// <param name="component">Current occurrence identity.</param>
    void RemoveComponent(ComponentId component);

    /// <summary>Restores an occurrence hidden by this instance or an ancestor.</summary>
    /// <param name="component">Hidden occurrence identity.</param>
    void RestoreComponent(ComponentId component);

    /// <summary>Clears an instance removal mask while preserving ancestor masks.</summary>
    /// <param name="component">Locally masked occurrence.</param>
    void ResetRemoval(ComponentId component);

    /// <summary>Discards uncommitted text and reloads the latest selected occurrence.</summary>
    void Cancel();

    /// <summary>Uses the document's existing engine Undo history.</summary>
    void Undo();

    /// <summary>Uses the document's existing engine Redo history.</summary>
    void Redo();

    /// <summary>Enables committed-revision preview or retries a failed revision.</summary>
    void ShowPreview();

    /// <summary>Fences and drains preview, preserving document/history and uncommitted input.</summary>
    /// <returns>Actual cleanup completion.</returns>
    Task StopPreviewAsync();
}
