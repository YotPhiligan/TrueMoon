using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Sessions;

namespace TrueMoon.Aether.Editor.UI;

public interface IEditorViewModel : System.ComponentModel.INotifyPropertyChanged
{
    SceneSnapshot Scene { get; }
    IReadOnlyList<string> EntityIds { get; }
    int EntityCount { get; }
    string? SelectedId { get; }

    /// <summary>Shows entity properties only while there is a selected entity context.</summary>
    bool IsInspectorVisible { get; }

    string SelectedName { get; }
    string StatusText { get; }
    string ErrorText { get; }

    /// <summary>Typed runtime state for presentation without parsing the legacy status string.</summary>
    SessionState RuntimeState { get; }

    /// <summary>Latest runtime tick, used by details.</summary>
    ulong Tick { get; }

    /// <summary>Runtime child process identity, when running.</summary>
    int? ProcessId { get; }

    /// <summary>Whether an explicit project/game is selected.</summary>
    bool HasProject { get; }

    /// <summary>Whether presentation has an actual project-session owner rather than a legacy scene-only workspace.</summary>
    bool HasSourceContext { get; }

    IReadOnlyList<KeyValuePair<string, string>> Properties { get; }
    bool CanBuild { get; }
    bool CanStart { get; }
    bool CanPlay { get; }

    /// <summary>Gets whether the title-bar Run action can start or resume simulation.</summary>
    bool CanRun { get; }

    bool CanPause { get; }
    bool CanStep { get; }
    bool CanStop { get; }
    bool CanReplace { get; }
    bool CanCancel { get; }
    void Select(string id);

    /// <summary>Ends entity inspection and discards the uncommitted selection draft.</summary>
    void ClearSelection();

    void Execute(EditorCommand command);

    /// <summary>Starts and plays a prepared runtime, or resumes it, with fresh admission checks.</summary>
    void RunSimulation();
}
