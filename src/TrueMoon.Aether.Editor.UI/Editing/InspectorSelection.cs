using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Editor.UI.Editing;

/// <summary>The authoring selection the inspector presents, without any display text.</summary>
/// <param name="EntityId">Selected entity, or null when nothing is selected.</param>
/// <param name="IsSceneOpen">Whether an authoring scene document is loaded.</param>
public sealed record InspectorSelection(EntityId? EntityId, bool IsSceneOpen);

/// <summary>Renders the selection as inspector guidance text.</summary>
public static class InspectorSelectionText
{
    /// <summary>Builds the caption: shortened entity id, or empty-selection / no-document guidance.</summary>
    public static string Describe(InspectorSelection selection) => selection.EntityId is { } id
        ? EditorScene.Name(id.Value.ToString("D"))
        : selection.IsSceneOpen ? "Сущность не выбрана" : "Нет открытой authoring-сцены";
}
