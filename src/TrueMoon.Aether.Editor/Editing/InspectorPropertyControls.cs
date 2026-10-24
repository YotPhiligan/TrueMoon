using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Typed retained parts of a scalar editor, keyed by component occurrence and property.</summary>
/// <param name="Property">Borrowed draft model.</param><param name="Row">Owned property subtree.</param>
/// <param name="Input">Typed CheckBox, TextBox or multiline RichTextEditor.</param><param name="Null">Optional explicit-null switch.</param>
/// <param name="Reset">Stage inheritance reset.</param><param name="Source">Bound provenance label.</param>
public sealed record InspectorPropertyControls(InspectorProperty Property, VStack Row, Element Input,
    CheckBox? Null, Button Reset, Text Source);

/// <summary>Typed parts of one component section.</summary>
/// <param name="Section">Owned section.</param><param name="Header">Expand/collapse action.</param>
/// <param name="Body">Collapsible fields.</param><param name="Remove">Explicit presence action.</param>
/// <param name="Metadata">Exact occurrence/type/schema information.</param>
public sealed record InspectorComponentControls(Border Section, Button Header, VStack Body, Button Remove, Text Metadata);
