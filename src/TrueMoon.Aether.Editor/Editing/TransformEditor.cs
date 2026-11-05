using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>A retained local Transform control; its parent binds the nine string drafts and owns its lifetime.</summary>
/// <remarks>Angles are pitch/yaw/roll in radians. This control does not commit changes or own the borrowed inspector model.</remarks>
public sealed class TransformEditor : VStack
{
    /// <summary>Local translation XYZ.</summary>
    public Vector3Editor Position { get; } = new("Позиция");
    /// <summary>Local pitch, yaw and roll, in radians.</summary>
    public Vector3Editor Rotation { get; } = new("Поворот, рад");
    /// <summary>Local positive scale XYZ.</summary>
    public Vector3Editor Scale { get; } = new("Масштаб");
    /// <summary>Opt-in proportional XYZ scale editing. The parent binds it to presentation state.</summary>
    public CheckBox LinkScale { get; } = new("Сохранять пропорции XYZ") { AccessibleName = "Связанный масштаб · сохранять пропорции XYZ", ColorRole = EditorColorRole.Boolean };
    /// <summary>Creates three retained vector rows without introducing engine mutation or history ownership.</summary>
    public TransformEditor()
    {
        Rotation.First.Prefix.Value = "P"; Rotation.Second.Prefix.Value = "Y"; Rotation.Third.Prefix.Value = "R";
        Add(Position); Add(Rotation); Add(Scale); Add(LinkScale);
    }
}
