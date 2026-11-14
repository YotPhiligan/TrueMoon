using System.Globalization;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Runtime-only presentation. Inspection shows captured scene data; selection belongs to this App, never a live authoring document.</summary>
public sealed class RuntimeEditorViewModel : ViewModel, IEditorViewModel
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, string>>> _properties;
    private SessionStatus _status;
    private string? _selected;
    private int _owner;
    private bool _busy, _closed;
    private Exception? _observedError;
    private static readonly string[] RuntimeProperties = [nameof(RuntimeState), nameof(Tick), nameof(ProcessId), nameof(StatusText), nameof(ErrorText),
        nameof(CanStart), nameof(CanPlay), nameof(CanRun), nameof(CanPause), nameof(CanStep), nameof(CanStop), nameof(CanCancel)];
    internal RuntimeEditorSession Session { get; }
    internal RuntimeEditorViewModel(PlayerStart start, RuntimeEditorSession session, SceneResolutionService resolution)
    {
        Session = session; _status = session.Status; _closed = session.IsClosed;
        Dictionary<string, IReadOnlyList<KeyValuePair<string, string>>> properties = new(StringComparer.Ordinal);
        if (start.World is { } world)
        {
            var resolved = resolution.Resolve(world.Scene);
            Scene = new(world.Scene.Id.Value.ToString("D"), world.Scene.Revision, []);
            EntityIds = Array.AsReadOnly(resolved.HierarchyOrder.Select(id => id.Value.ToString("D")).ToArray());
            foreach (var entity in resolved.Entities.Values)
            {
                List<KeyValuePair<string, string>> values = [new("ID", entity.Id.Value.ToString("D")), new("Source", "Captured scene (read only)")];
                foreach (var component in entity.Components.Values.OrderBy(item => item.Id.Value))
                {
                    values.Add(new("Component", component.TypeId.Value.ToString("D")));
                    foreach (var property in component.Properties.OrderBy(item => item.Key, StringComparer.Ordinal))
                        values.Add(new(property.Key, Format(property.Value.Value)));
                }
                properties.Add(entity.Id.Value.ToString("D"), values.AsReadOnly());
            }
        }
        else
        {
            Scene = start.Snapshot!;
            var entities = Scene.CopyEntities(); EntityIds = Array.AsReadOnly(entities.Select(entity => entity.Id).ToArray());
            foreach (var entity in entities)
                properties.Add(entity.Id, Array.AsReadOnly(new KeyValuePair<string, string>[]
                {
                    new("ID", entity.Id), new("Source", "Captured scene (read only)"),
                    new("X", entity.X.ToString("R", CultureInfo.InvariantCulture)), new("Y", entity.Y.ToString("R", CultureInfo.InvariantCulture)),
                    new("Z", entity.Z.ToString("R", CultureInfo.InvariantCulture))
                }));
        }
        _properties = properties;
    }
    public SceneSnapshot Scene { get; }
    public IReadOnlyList<string> EntityIds { get; }
    public int EntityCount => EntityIds.Count;
    public string? SelectedId => _selected;
    public bool IsInspectorVisible => _selected != null;
    public string SelectedName => _selected == null ? "No selection" : EditorScene.Name(_selected);
    public string StatusText => $"Runtime · {_status.State} · Tick {_status.Tick} · PID {_status.ProcessId?.ToString() ?? "—"}";
    public string ErrorText => Session.Error?.Message ?? _status.Failure?.Cause.Message ?? "";
    public SessionState RuntimeState => _status.State;
    public ulong Tick => _status.Tick;
    public int? ProcessId => _status.ProcessId;
    public bool HasProject => false;
    public bool HasSourceContext => false;
    public IReadOnlyList<KeyValuePair<string, string>> Properties => _selected is { } id && _properties.TryGetValue(id, out var rows) ? rows : [];
    public bool CanBuild => false;
    public bool CanReplace => false;
    public bool CanStart => Session.CanExecute(EditorCommand.Start);
    public bool CanPlay => Session.CanExecute(EditorCommand.Play);
    public bool CanRun => CanStart || CanPlay;
    public bool CanPause => Session.CanExecute(EditorCommand.Pause);
    public bool CanStep => Session.CanExecute(EditorCommand.Step);
    public bool CanStop => Session.CanExecute(EditorCommand.Stop);
    public bool CanCancel => Session.CanExecute(EditorCommand.Cancel);
    public void Select(string id)
    {
        VerifyOwner(); ArgumentNullException.ThrowIfNull(id);
        if (!_properties.ContainsKey(id)) throw new ArgumentException("Entity is outside the captured scene.", nameof(id));
        if (_selected == id) return;
        _selected = id; SelectionChanged();
    }
    public void ClearSelection() { VerifyOwner(); if (_selected == null) return; _selected = null; SelectionChanged(); }
    private void SelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedId)); OnPropertyChanged(nameof(SelectedName));
        OnPropertyChanged(nameof(IsInspectorVisible)); OnPropertyChanged(nameof(Properties));
    }
    public void Execute(EditorCommand command) { VerifyOwner(); Session.Execute(command); Refresh(); }
    public void RunSimulation() { VerifyOwner(); Session.RunSimulation(); Refresh(); }
    /// <summary>Copies runtime status and capability changes on the UI owner thread.</summary>
    public void Refresh()
    {
        VerifyOwner(); var status = Session.Status; var busy = !Session.Pending.IsCompleted; var error = Session.Error; var closed = Session.IsClosed;
        if (status == _status && busy == _busy && closed == _closed && ReferenceEquals(error, _observedError)) return;
        _status = status; _busy = busy; _closed = closed; _observedError = error;
        foreach (var name in RuntimeProperties) OnPropertyChanged(name);
    }
    private static string Format(PropertyValue value) => value.Kind switch
    {
        PropertyValueKind.Null => "null", PropertyValueKind.Boolean => value.GetBoolean() ? "true" : "false",
        PropertyValueKind.Integer => value.GetInteger().ToString(CultureInfo.InvariantCulture),
        PropertyValueKind.Number => value.GetNumber().ToString("R", CultureInfo.InvariantCulture),
        _ => System.Text.Json.JsonSerializer.Serialize(value.GetString())
    };
    private void VerifyOwner()
    {
        var current = Environment.CurrentManagedThreadId; var owner = Interlocked.CompareExchange(ref _owner, current, 0);
        if (owner != 0 && owner != current) throw new InvalidOperationException("Runtime editor presentation must stay on its UI owner thread.");
    }
}
