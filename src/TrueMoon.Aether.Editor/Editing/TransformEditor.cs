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
    /// <summary>Transient rotation display preference; canonical values remain radians.</summary>
    public CheckBox Degrees { get; } = new("Углы в градусах")
    { AccessibleName = "Углы в градусах · выключено: радианы; шаг 0.01 рад или 1°", ColorRole = EditorColorRole.Boolean };
    /// <summary>Stages a reset of the Position group.</summary>
    public IconButton ResetPosition { get; } = new(UiSymbol.Undo, "Сбросить группу Позиция к наследуемым значениям или умолчаниям");
    /// <summary>Stages a reset of the Rotation group.</summary>
    public IconButton ResetRotation { get; } = new(UiSymbol.Undo, "Сбросить группу Поворот к наследуемым значениям или умолчаниям");
    /// <summary>Stages a reset of the Scale group.</summary>
    public IconButton ResetScale { get; } = new(UiSymbol.Undo, "Сбросить группу Масштаб к наследуемым значениям или умолчаниям");
    /// <summary>Opt-in proportional XYZ scale editing. The parent binds it to presentation state.</summary>
    public CheckBox LinkScale { get; } = new("Сохранять пропорции XYZ") { AccessibleName = "Связанный масштаб · сохранять пропорции XYZ", ColorRole = EditorColorRole.Boolean };
    /// <summary>Creates three retained vector rows without introducing engine mutation or history ownership.</summary>
    public TransformEditor()
    {
        Rotation.First.Prefix.Value = "P"; Rotation.Second.Prefix.Value = "Y"; Rotation.Third.Prefix.Value = "R";
        Add(new GroupRow(Position, ResetPosition)); Add(new GroupRow(Rotation, ResetRotation)); Add(Degrees);
        Add(new GroupRow(Scale, ResetScale)); Add(LinkScale);
    }

    // Reserve the action's width before measuring the responsive vector, including in narrow panes.
    private sealed class GroupRow : HStack
    {
        public GroupRow(Vector3Editor vector, IconButton reset)
        { this.WithRole(EditorInspectorView.TransformGroupRole); Add(vector); Add(reset); }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            Children[1].Measure(available, text);
            Children[0].Measure(new(Math.Max(0, available.Width - Children[1].DesiredSize.Width - Spacing), available.Height), text);
            return new(Children[0].DesiredSize.Width + Spacing + Children[1].DesiredSize.Width,
                Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height));
        }
        protected override void ArrangeCore(Rect area)
        {
            var actionWidth = Math.Min(area.Width, Children[1].DesiredSize.Width);
            var vectorWidth = Math.Max(0, area.Width - actionWidth - Spacing);
            Children[0].Arrange(new(area.X, area.Y, vectorWidth, area.Height));
            var actionHeight = Math.Min(area.Height, Children[1].DesiredSize.Height);
            Children[1].Arrange(new(area.X + area.Width - actionWidth, area.Y, actionWidth, actionHeight));
        }
    }
}
