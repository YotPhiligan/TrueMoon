namespace TrueMoon.Aether.Editor.Diagnostics;

public sealed partial class EditorDiagnosticsViewModel
{
    private CancellationTokenSource? _exportCancellation;
    private bool _exportIncludesText, _exportIncludesHistory = true, _exportTextAtStart;
    private EditorDiagnosticPresentation? _presentation;

    /// <summary>Whether the next report explicitly includes producer text and source paths.</summary>
    public bool ExportIncludesText
    {
        get => _exportIncludesText;
        set { VerifyOpen(); if (IsExporting) throw new InvalidOperationException("Export options are frozen while a report is running.");
            if (_exportIncludesText == value) return; _exportIncludesText = value; OnPropertyChanged(nameof(ExportIncludesText)); OnPropertyChanged(nameof(ExportPrivacyNotice)); }
    }
    /// <summary>Whether the next report reads Core's configured persistent JSONL history.</summary>
    public bool ExportIncludesHistory
    {
        get => _exportIncludesHistory;
        set { VerifyOpen(); if (IsExporting) throw new InvalidOperationException("Export options are frozen while a report is running.");
            if (_exportIncludesHistory == value) return; _exportIncludesHistory = value; OnPropertyChanged(nameof(ExportIncludesHistory)); }
    }
    /// <summary>Whether an export can still accept a user cancellation request.</summary>
    public bool CanCancelExport => IsExporting && _exportCancellation is { IsCancellationRequested: false };
    /// <summary>Whether history/text controls can be changed.</summary>
    public bool CanChangeExportOptions => !IsExporting && !_lifetime.IsCancellationRequested;
    /// <summary>Explicit data-sharing policy for the chosen report.</summary>
    public string ExportPrivacyNotice => ExportIncludesText ? "Текст может содержать личные пути и данные приложения." : "Без текста сообщений, стеков, raw-вывода и путей исходников.";

    /// <summary>Requests cooperative cancellation. Admission remains closed until the accepted task has drained.</summary>
    public void CancelExport()
    {
        VerifyOpen(); if (!CanCancelExport) return; _exportCancellation!.Cancel();
        _exportStatus = "Отмена экспорта…"; OnPropertyChanged(nameof(ExportStatus)); OnPropertyChanged(nameof(CanCancelExport));
    }

    /// <summary>Copies a value-only presentation observation on the UI owner; no renderer/device is retained.</summary>
    /// <param name="presentation">Observed backend and viewport; null clears the capability.</param>
    public void ObservePresentation(EditorDiagnosticPresentation? presentation)
    {
        VerifyOpen();
        if (presentation != null && (presentation.Width < 0 || presentation.Height < 0 || !float.IsFinite(presentation.Scale) || presentation.Scale <= 0))
            throw new ArgumentException("Invalid presentation viewport.", nameof(presentation));
        _presentation = presentation == null ? null : presentation with
        { Backend = Display(presentation.Backend, 256), Device = presentation.Device == null ? null : Display(presentation.Device, 256) };
    }

    private void ExportChanged()
    {
        OnPropertyChanged(nameof(ExportStatus)); OnPropertyChanged(nameof(IsExporting)); OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanCancelExport)); OnPropertyChanged(nameof(CanChangeExportOptions));
    }
}
