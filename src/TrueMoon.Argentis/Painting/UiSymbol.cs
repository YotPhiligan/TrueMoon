using TrueMoon.Argentis.Styling;

namespace TrueMoon.Argentis.Painting;

/// <summary>Small vector symbols, independent of font glyph availability.</summary>
public enum UiSymbol
{
    /// <summary>Minimize line.</summary>
    Minimize,
    /// <summary>Maximize outline.</summary>
    Maximize,
    /// <summary>Overlapping restore outlines.</summary>
    Restore,
    /// <summary>Close cross.</summary>
    Close,
    /// <summary>Checked-state tick.</summary>
    Check,
    /// <summary>Bottom-right sizing marks.</summary>
    ResizeGrip,
    /// <summary>Three horizontal menu dots.</summary>
    More,
    /// <summary>Play triangle.</summary>
    Play,
    /// <summary>Pause bars.</summary>
    Pause,
    /// <summary>Stop square.</summary>
    Stop,
    /// <summary>File outline.</summary>
    File,
    /// <summary>Open folder.</summary>
    Folder,
    /// <summary>Save disk.</summary>
    Save,
    /// <summary>Undo arrow.</summary>
    Undo,
    /// <summary>Redo arrow.</summary>
    Redo,
    /// <summary>Build/tool mark.</summary>
    Build,
    /// <summary>Theme half-disc.</summary>
    Theme,
    /// <summary>Forward single step.</summary>
    Step,
    /// <summary>TrueMoon's silver Argentis moon.</summary>
    TrueMoon,
    /// <summary>Information marker.</summary>
    Info,
    /// <summary>Error/warning marker, distinct from normal status.</summary>
    Warning,
    /// <summary>Busy hourglass.</summary>
    Busy,
    /// <summary>Scene objects.</summary>
    Objects,
    /// <summary>Preview eye.</summary>
    Preview,
    /// <summary>Generic scene occurrence.</summary>
    Entity,
    /// <summary>Renderable cube.</summary>
    Cube,
    /// <summary>Interactive occurrence marker.</summary>
    Interaction,
    /// <summary>Collapsed branch.</summary>
    ChevronRight,
    /// <summary>Expanded branch.</summary>
    ChevronDown,
    /// <summary>Add a new item.</summary>
    Add,
    /// <summary>Delete an item.</summary>
    Delete,
    /// <summary>Horizontal numeric adjustment.</summary>
    AdjustHorizontal
}