using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Stable numeric Transform projection retained for existing form consumers.</summary>
public sealed class TransformField : InspectorProperty
{
    internal TransformField(string key, string label, Action changed)
        : base(default, new(key, PropertyValueKind.Number, PropertyValue.From(key is "sx" or "sy" or "sz" ? 1d : 0d)), label, changed) { }
}
