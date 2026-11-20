using System.ComponentModel;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Editing;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.SourceTracking;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Tooling;

/// <summary>Semantic primary status; styling and icon selection belong to the view.</summary>




/// <summary>App-owned status presentation borrowing the existing runtime/document/preview models.</summary>
public sealed class EditorStatusBarViewModel : ViewModel, IDisposable, IEditorStatusBarViewModel
{
    private readonly EditorViewModel _editor;
    private readonly EditorDocumentViewModel _documents;
    private readonly EditorInspectorViewModel _transform;
    private bool _disposed;
    private string _summary = "", _entities = "", _runtime = "", _operation = "", _preview = "", _previewDetails = "", _error = "";
    private EditorStatusKind _kind;
    private bool _canCancel, _canPreview, _canStopPreview;
    private string _sourceSummary = "", _sourceDetails = "", _modelImportDetails = "";
    private EditorStatusKind _sourceKind;
    private bool _hasSource;
    private IReadOnlyList<SceneModelImportStatus> _modelImports = [];
    private IReadOnlyList<EditorModelReimportAction> _modelReimports = [];
    private Guid _importOpening;
    private bool _canReimport;
    private bool _hasModelCatalog, _canReadModelCatalog;
    private string _catalogMessage = "";
    private IReadOnlyList<ProjectModelCatalogItem>? _catalogItems;
    private IReadOnlyList<SceneModelImportStatus> _catalogImports = [];
    private IReadOnlyList<EditorProjectModelRow> _projectModels = [];
    private Guid _catalogOpening;
    private bool _canChangeCatalog;
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
    /// <summary>Independent source badge; it does not replace the primary runtime/command status.</summary>
    public string SourceSummary => _sourceSummary;
    /// <summary>Last observation, baseline and coverage details.</summary>
    public string SourceDetails => _sourceDetails;
    /// <summary>Advisory status for the active document's model imports.</summary>
    public string ModelImportDetails => _modelImportDetails;
    /// <summary>Whether the active document has selected model imports.</summary>
    public bool HasModelImports => _modelImportDetails.Length != 0;
    /// <inheritdoc />
    public IReadOnlyList<EditorModelReimportAction> ModelReimports => _modelReimports;
    /// <inheritdoc />
    public bool HasModelCatalog => _hasModelCatalog;
    /// <inheritdoc />
    public bool CanReadModelCatalog => _canReadModelCatalog;
    /// <inheritdoc />
    public string ModelCatalogMessage => _catalogMessage;
    /// <inheritdoc />
    public IReadOnlyList<EditorProjectModelRow> ProjectModels => _projectModels;
    /// <summary>Source-specific semantic styling.</summary>
    public EditorStatusKind SourceKind => _sourceKind;
    /// <summary>Whether the current project has a source segment.</summary>
    public bool HasSource => _hasSource;
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
        RefreshSource();
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
            summary = _documents.Operation switch
            {
                DocumentCommand.Save or DocumentCommand.SaveAs => "Сохранение сцены…",
                DocumentCommand.Open => "Открытие сцены…",
                DocumentCommand.ReimportModel => "Переимпорт модели…",
                DocumentCommand.ImportModel => "Импорт модели…",
                DocumentCommand.ReadModelCatalog => "Чтение каталога моделей…",
                DocumentCommand.PlaceModel => "Добавление модели в сцену…",
                DocumentCommand.SelectModel => "Обновление выбора моделей…",
                _ => "Операция с документом…"
            };
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
        var opening = _documents.Document?.OpeningId ?? Guid.Empty;
        var imports = _editor.ModelImports;
        var enabled = _documents.CanReimportModel;
        if (opening != _importOpening || !ReferenceEquals(imports, _modelImports) || enabled != _canReimport)
        {
            _importOpening = opening; _modelImports = imports; _canReimport = enabled;
            SetProperty(ref _modelReimports, Array.AsReadOnly(imports.Where(model => model.CanReimport).Select(model =>
                new EditorModelReimportAction(opening, model.SourceId, model.Generation, model.Name, enabled)).ToArray()), nameof(ModelReimports));
        }
        RefreshCatalog(opening, imports, _documents.CanChangeModelCatalog);
    }

    private void RefreshCatalog(Guid opening, IReadOnlyList<SceneModelImportStatus> imports, bool canChange)
    {
        var items = _documents.ModelCatalog;
        SetProperty(ref _hasModelCatalog, _documents.HasModelCatalog, nameof(HasModelCatalog));
        SetProperty(ref _canReadModelCatalog, _documents.CanReadModelCatalog, nameof(CanReadModelCatalog));
        var message = _documents.Operation == DocumentCommand.ReadModelCatalog ? "Чтение каталога…" : items == null ? "" :
            items.Count == 0 && imports.Count == 0 ? "В каталог ещё не добавлены модели." :
            opening == Guid.Empty ? "Откройте сцену, чтобы выбрать модели." : "";
        SetProperty(ref _catalogMessage, message, nameof(ModelCatalogMessage));
        if (ReferenceEquals(items, _catalogItems) && ReferenceEquals(imports, _catalogImports) &&
            opening == _catalogOpening && canChange == _canChangeCatalog) return;
        _catalogItems = items; _catalogImports = imports; _catalogOpening = opening; _canChangeCatalog = canChange;
        if (items == null)
        {
            SetProperty(ref _projectModels, [], nameof(ProjectModels));
            return;
        }
        var selected = imports.ToDictionary(model => model.SourceId);
        var registered = items.Select(model => model.SourceId).ToHashSet();
        SetProperty(ref _projectModels, Array.AsReadOnly(items.Select(item =>
            CreateRow(item.SourceId, item.Name, item.SourcePath, selected.GetValueOrDefault(item.SourceId)))
            .Concat(imports.Where(item => !registered.Contains(item.SourceId)).Select(item => CreateRow(item.SourceId, item.Name, "", item)))
            .ToArray()), nameof(ProjectModels));

        EditorProjectModelRow CreateRow(Guid sourceId, string name, string path, SceneModelImportStatus? selection) =>
            new(opening, sourceId, name, path, selection != null, canChange) { SelectedGeneration = selection?.Generation };
    }
    private void RefreshSource()
    {
        var source = _editor.Source;
        var hadImports = HasModelImports;
        SetProperty(ref _modelImportDetails, _editor.ModelImportDetails, nameof(ModelImportDetails));
        if (hadImports != HasModelImports) OnPropertyChanged(nameof(HasModelImports));
        var state = source?.State ?? EditorSourceState.Unavailable;
        var summary = !_editor.HasProject ? "Проект не открыт" : state switch
        {
            EditorSourceState.NoBuild => "Сборка не создана",
            EditorSourceState.Checking => "Проверяются исходники",
            EditorSourceState.ObservedUnchanged => "Изменения исходников не обнаружены",
            EditorSourceState.Changed => "Исходники изменены — требуется Build",
            EditorSourceState.Stopped => "Наблюдение остановлено",
            _ => "Состояние исходников неизвестно"
        };
        var kind = state == EditorSourceState.Checking ? EditorStatusKind.Busy :
            state is EditorSourceState.Changed or EditorSourceState.Unavailable ? EditorStatusKind.Waiting : EditorStatusKind.Ready;
        var details = source == null ? (_editor.HasProject ? "Наблюдение исходников не подключено." : "Проект не открыт.") :
            summary + "\nBuild: " + source.BuildId + "\nПоследнее наблюдение: " +
            (source.ObservedAt?.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture) ?? "ещё не завершено") +
            "\nРежим: " + (source.Mode == EditorSourceObservationMode.NotificationsAndPolling ? "уведомления и полный опрос" : "полный опрос") +
            (source.IsScanning ? " · проверка выполняется" : "") + (source.Reason == null ? "" : "\n" + source.Reason) +
            (source.ChangedPaths.Count == 0 ? "" : "\nИзменённые пути:\n" + string.Join("\n", source.ChangedPaths));
        SetProperty(ref _hasSource, _editor.HasProject && _editor.HasSourceContext, nameof(HasSource));
        SetProperty(ref _sourceSummary, summary, nameof(SourceSummary));
        SetProperty(ref _sourceDetails, details, nameof(SourceDetails));
        SetProperty(ref _sourceKind, kind, nameof(SourceKind));
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
    /// <inheritdoc />
    public void ReimportModel(EditorModelReimportAction action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (action.Enabled) _documents.ReimportModel(action);
    }
    /// <inheritdoc />
    public void ReadModelCatalog() { ObjectDisposedException.ThrowIf(_disposed, this); _documents.ReadModelCatalog(); }
    /// <inheritdoc />
    public void PlaceModel(EditorProjectModelRow row) { ObjectDisposedException.ThrowIf(_disposed, this); _documents.PlaceModel(row); }
    /// <inheritdoc />
    public void SelectModel(EditorProjectModelRow row) { ObjectDisposedException.ThrowIf(_disposed, this); if (row.CanChange) _documents.SelectModel(row); }
    /// <summary>Disconnects subscriptions; the injected models remain App-owned.</summary>
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _editor.PropertyChanged -= Changed; _documents.PropertyChanged -= Changed; _transform.PropertyChanged -= Changed;
        ClearPropertyChangedSubscribers();
    }
}
