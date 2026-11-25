using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Editor.UI.Editing;
public static class InspectorComponentNames
{
    public static string Get(ComponentTypeId type) => type == BuiltInComponents.TransformId ? "Transform" :
        type == BuiltInComponents.AppearanceId ? "Appearance" : type == BuiltInComponents.InteractionId ? "Interaction" :
        "Component " + type.Value.ToString("D")[..8];
}
