namespace TrueMoon.Alloy.Runtime.Semantics;

/// <summary>Common semantic roles of retained UI elements.</summary>
public enum UiSemanticRole
{
    /// <summary>Layout container or element without a more specific role.</summary>
    Group,
    /// <summary>Label or other display text.</summary>
    Text,
    /// <summary>Invokable action control.</summary>
    Button,
    /// <summary>Checkable action control.</summary>
    CheckBox,
    /// <summary>Single-line or multiline editable text control.</summary>
    TextInput,
    /// <summary>Composite numeric editor.</summary>
    NumberInput,
    /// <summary>Tree control with drawn rows.</summary>
    Tree,
    /// <summary>Scroll viewport.</summary>
    Scroll,
    /// <summary>Transient overlay content.</summary>
    Popup
}
