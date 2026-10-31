using System.ComponentModel;
using TrueMoon.Argentis;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Editor;

/// <summary>Semantic primary status; styling and icon selection belong to the view.</summary>
public enum EditorStatusKind { Ready, Busy, Paused, Running, Error, Waiting }

/// <summary>App-owned status presentation borrowing the existing runtime/document/preview models.</summary>
public sealed class EditorStatusBarViewModel : ViewModel, IDisposable
{
    private readonly EditorViewModel _editor;
    private readonly EditorDocumentViewModel _documents;
    private readonly EditorInspectorViewModel _transform;
    private bool _disposed;
    private string _summary = "", _entities = "", _runtime = "", _operation = "", _preview = "", _previewDetails = "", _error = "";
    private EditorStatusKind _kind;
    private bool _canCancel, _canPreview, _canStopPreview;
    /// <summary>Creates presentation without taking ownership of the injected models.</summary>
    public EditorStatusBarViewModel(EditorViewModel editor, EditorDocumentViewModel documents, EditorInspectorViewModel transform)
    {
        ArgumentNullException.ThrowIfNull(editor); ArgumentNullException.ThrowIfNull(documents); ArgumentNullException.ThrowIfNull(transform);
        _editor = editor; _documents = documents; _transform = transform;
        Refresh();
        editor.PropertyChanged += Changed; documents.PropertyChanged += Changed; transform.PropertyChanged += Changed;
    }
    /// <summary>Short primary status.</summary>
    public string Summary => _summary;
    /// <summary>Semantic status independent of UI colors and icons.</summary>
    public EditorStatusKind Kind => _kind;
    /// <summary>Scene occurrence count.</summary>
    public string EntityText => _entities;
    /// <summary>Runtime state, tick and process identity for the details panel.</summary>
    public string RuntimeDetails => _runtime;
    /// <summary>Current game operation details.</summary>
    public string OperationDetails => _operation;
    /// <summary>Compact preview activity.</summary>
    public string PreviewSummary => _preview;
    /// <summary>Actual preview revision and pending work.</summary>
    public string PreviewDetails => _previewDetails;
    /// <summary>Complete runtime/document/preview diagnostics.</summary>
    public string ErrorDetails => _error;
    /// <summary>Whether diagnostics are available.</summary>
    public bool HasError => _error.Length != 0;
    /// <summary>Whether current work can be cancelled.</summary>
    public bool CanCancel => _canCancel;
    /// <summary>Existing committed-document preview admission.</summary>
    public bool CanPreview => _canPreview;
    /// <summary>Existing preview stop/recovery admission.</summary>
    public bool CanStopPreview => _canStopPreview;

    private void Changed(object? sender, PropertyChangedEventArgs args) { if (!_disposed) Refresh(); }
    private void Refresh()
    {
        var preview = _transform.PreviewState;
        var runtime = _editor.RuntimeState switch
        {
            SessionState.Stopped => "Остановлена", SessionState.Starting => "Запуск", SessionState.Paused => "Пауза",
            SessionState.Playing => "Симуляция", SessionState.Stopping => "Остановка", _ => "Ошибка runtime"
        };
        var operation = _editor.Operation switch
        {
            GameEditorOperation.Idle => "Готово", GameEditorOperation.Build => "Сборка проекта…",
            GameEditorOperation.Replace => "Применение сборки…", GameEditorOperation.Stop => "Остановка…", _ => "Выполняется команда…"
        };
        var errors = new[] { _editor.ErrorText, _documents.ErrorText, preview.Error?.Message ?? "" }
            .Where(s => s.Length != 0).Distinct().ToArray();
        var hadError = HasError;
        SetProperty(ref _error, string.Join("\n\n", errors).ReplaceLineEndings("\n"), nameof(ErrorDetails));
        if (hadError != HasError) OnPropertyChanged(nameof(HasError));
        var kind = EditorStatusKind.Ready;
        var summary = _editor.HasProject ? "Готово" : "Проект не открыт";
        if (_documents.RuntimeOperation != GameEditorOperation.Idle)
        {
            kind = EditorStatusKind.Busy;
            summary = _documents.RuntimeOperation switch
            { GameEditorOperation.Build => "Сборка сцены…", GameEditorOperation.Start => "Подготовка и запуск сцены…", _ => "Команда игры…" };
        }
        else if (_documents.AwaitingDecision) { kind = EditorStatusKind.Waiting; summary = "Выберите: сохранить или отменить"; }
        else if (_documents.IsBusy)
        {
            kind = EditorStatusKind.Busy;
            summary = _documents.Operation is DocumentCommand.Save or DocumentCommand.SaveAs ? "Сохранение сцены…" :
                _documents.Operation == DocumentCommand.Open ? "Открытие сцены…" : "Операция с документом…";
        }
        else if (_editor.Operation != GameEditorOperation.Idle) { kind = EditorStatusKind.Busy; summary = operation; }
        else if (preview.Busy) { kind = EditorStatusKind.Busy; summary = "Обновление Edit Preview…"; }
        else if (HasError || _editor.RuntimeState is SessionState.Faulted or SessionState.CleanupFailed)
        { kind = EditorStatusKind.Error; summary = _documents.ErrorText.Length != 0 ? "Ошибка документа" : preview.Error != null ? "Ошибка Edit Preview" : "Ошибка runtime / команды"; }
        else if (preview.Active) { summary = "Редактирование сцены"; }
        else if (_editor.RuntimeState == SessionState.Playing) { kind = EditorStatusKind.Running; summary = "Симуляция"; }
        else if (_editor.RuntimeState == SessionState.Paused) { kind = EditorStatusKind.Paused; summary = "Симуляция на паузе"; }
        else if (_editor.RuntimeState is SessionState.Starting or SessionState.Stopping) { kind = EditorStatusKind.Busy; summary = runtime + "…"; }
        SetProperty(ref _kind, kind, nameof(Kind)); SetProperty(ref _summary, summary, nameof(Summary));
        SetProperty(ref _entities, $"{_editor.EntityCount} объектов", nameof(EntityText));
        SetProperty(ref _runtime, $"{(preview.Active ? "Runtime Edit Preview" : "Runtime")}: {runtime} · Tick {_editor.Tick} · PID {_editor.ProcessId?.ToString() ?? "—"}", nameof(RuntimeDetails));
        SetProperty(ref _operation, "Операция: " + (kind is EditorStatusKind.Busy or EditorStatusKind.Waiting ? summary : operation), nameof(OperationDetails));
        SetProperty(ref _preview, preview.Error != null ? "Edit Preview: ошибка" : preview.Busy ? "Edit Preview: обновление…" :
            preview.Active ? "Edit Preview: вкл." : "Edit Preview: выкл.", nameof(PreviewSummary));
        SetProperty(ref _previewDetails, _transform.PreviewText, nameof(PreviewDetails));
        SetProperty(ref _canCancel, _documents.CanCancel || _editor.CanCancel || preview.Busy && _transform.CanStopPreview, nameof(CanCancel));
        SetProperty(ref _canPreview, _transform.CanPreview, nameof(CanPreview));
        SetProperty(ref _canStopPreview, _transform.CanStopPreview, nameof(CanStopPreview));
    }
    /// <summary>Cancels the active document/game operation, or drains a pending preview.</summary>
    public Task CancelAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_documents.CanCancel) _documents.Cancel();
        else if (_editor.CanCancel) _editor.Execute(EditorCommand.Cancel);
        else if (_transform.PreviewState.Busy && _transform.CanStopPreview) return _transform.StopPreviewAsync();
        return Task.CompletedTask;
    }
    /// <summary>Uses the existing guarded preview command.</summary>
    public void ShowPreview() { ObjectDisposedException.ThrowIf(_disposed, this); _transform.ShowPreview(); }
    /// <summary>Fences and drains the existing preview owner.</summary>
    public Task StopPreviewAsync() { ObjectDisposedException.ThrowIf(_disposed, this); return _transform.StopPreviewAsync(); }
    /// <summary>Disconnects subscriptions; the injected models remain App-owned.</summary>
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _editor.PropertyChanged -= Changed; _documents.PropertyChanged -= Changed; _transform.PropertyChanged -= Changed;
        ClearPropertyChangedSubscribers();
    }
}
