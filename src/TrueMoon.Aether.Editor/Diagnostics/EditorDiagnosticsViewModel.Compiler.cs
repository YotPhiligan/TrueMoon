using System.Text;
using TrueMoon.Aether.Builds;

namespace TrueMoon.Aether.Editor.Diagnostics;

public sealed partial class EditorDiagnosticsViewModel
{
    private readonly ISourceFileNavigator? _navigator;
    private EditorSourceLocation? _selectedSource;
    private Task? _navigation;
    private string _navigationStatus = "";

    /// <summary>Whether the selected compiler diagnostic can be sent to the configured source editor.</summary>
    public bool CanOpenSource => Mode == EditorDiagnosticMode.Compiler && _selectedSource != null && _navigator != null &&
        _navigation is not { IsCompleted: false } && !_lifetime.IsCancellationRequested;
    /// <summary>Observed navigation outcome; completion acknowledges a request rather than another application's UI.</summary>
    public string NavigationStatus => _navigationStatus;

    /// <summary>Starts one explicit request over the frozen selected location. No file IO or process launch runs on the UI owner.</summary>
    public void OpenSource()
    {
        VerifyOpen(); if (!CanOpenSource) return;
        _navigationStatus = "Открытие исходника…";
        try { _navigation = _navigator!.OpenAsync(_selectedSource!, _lifetime.Token); }
        catch (Exception error) { _navigation = Task.FromException(error); }
        ObserveNavigation(); Changed(nameof(CanOpenSource)); Changed(nameof(NavigationStatus));
    }

    private void ObserveNavigation()
    {
        if (_navigation is not { IsCompleted: true } completed) return;
        _navigation = null;
        try { completed.GetAwaiter().GetResult(); _navigationStatus = "Запрос на открытие исходника отправлен."; }
        catch (OperationCanceledException) { _navigationStatus = "Открытие исходника отменено."; }
        catch (Exception error) { _navigationStatus = "Исходник не открыт: " + error.GetType().Name; }
        Changed(nameof(CanOpenSource)); Changed(nameof(NavigationStatus));
    }

    private IEnumerable<EditorDiagnosticRow> CompilerRows()
    {
        foreach (var process in _processes?.Processes.Where(MatchesProcess) ?? [])
        {
            if (process.CompilerDiagnostics is not { } batch) continue;
            yield return new(process.Id + ":compiler", $"Compiler · job {process.JobId} · revision {process.SourceRevision} · {batch.State} · " +
                $"{batch.Entries.Count} diagnostics · omitted {batch.Omitted} · truncated {batch.IsTruncated}");
            for (var index = 0; index < batch.Entries.Count; index++)
            {
                var entry = batch.Entries[index];
                if (_search.Length > 0 && !Contains(entry.Code + " " + entry.Message + " " + entry.FilePath, _search)) continue;
                yield return new(process.Id + ":compiler:" + index,
                    $"{entry.Severity} {entry.Code} · {entry.FilePath}:{entry.Line}:{entry.Column} · {entry.Message}".ReplaceLineEndings(" "))
                { Warning = entry.Severity is GameBuildDiagnosticSeverity.Error or GameBuildDiagnosticSeverity.Warning };
            }
        }
    }

    private void SelectCompiler(string? id)
    {
        _selectedSource = null; _contextSelection = null; _inspectedContext = "";
        foreach (var process in _processes?.Processes ?? [])
        {
            if (process.CompilerDiagnostics is not { } batch) continue;
            var text = new StringBuilder($"COMPILER SARIF2.1 · job {process.JobId} · revision {process.SourceRevision}\nBuild {process.GameBuildId}\nCapture {batch.State}; omitted {batch.Omitted}; truncated {batch.IsTruncated}; failure {batch.FailureType}\n");
            if (id == process.Id + ":compiler") { _contextSelection = id; _inspectedContext = text.ToString(); break; }
            for (var index = 0; index < batch.Entries.Count; index++)
            {
                if (id != process.Id + ":compiler:" + index) continue;
                var entry = batch.Entries[index]; _contextSelection = id;
                _inspectedContext = text.AppendLine($"{entry.Severity} {entry.Code}").AppendLine(entry.Message)
                    .AppendLine($"{entry.FilePath}:{entry.Line}:{entry.Column}")
                    .AppendLine(_navigator == null ? "Переход недоступен: host не подключил source editor hook." : "Переход использует координаты сохранённой диагностики; исходник мог измениться.").ToString();
                if (entry.FilePath is { } path && entry.Line is int line && line > 0 && entry.Column is int column && column > 0)
                    _selectedSource = new(path, line, column);
                break;
            }
            if (_contextSelection != null) break;
        }
        _inspectedContext = Display(_inspectedContext, 12288);
        UpdateDetails(); Changed(nameof(CanOpenSource)); Changed(nameof(CanRefreshDetails));
    }

    private bool HasCompilerSelection(string id) => _processes?.Processes.Any(process => process.CompilerDiagnostics is { } batch &&
        (id == process.Id + ":compiler" || Enumerable.Range(0, batch.Entries.Count).Any(index => id == process.Id + ":compiler:" + index))) == true;

    private GameBuildDiagnosticBatch? BoundCompiler(GameBuildDiagnosticBatch? batch, ref int remaining, ref int bytes)
    {
        if (batch == null) return null;
        var entries = new List<GameBuildDiagnostic>(); var truncated = batch.IsTruncated;
        foreach (var entry in batch.Entries.Take(128))
        {
            var code = Display(entry.Code, 128); var message = Display(entry.Message, 2048);
            var path = entry.FilePath is { Length: <= 4096 } p && Path.IsPathFullyQualified(p) ? p : null;
            var cost = 128 + 2 * (code.Length + message.Length + (path?.Length ?? 0));
            if (remaining == 0 || cost > bytes) break;
            truncated |= code.Length != entry.Code.Length || message.Length != entry.Message.Length || path != entry.FilePath;
            entries.Add(entry with { Code = code, Message = message, FilePath = path }); remaining--; bytes -= cost;
        }
        var omitted = batch.Entries.Count - entries.Count;
        return batch with { Entries = entries.AsReadOnly(), Omitted = (int)Math.Min(int.MaxValue, (long)batch.Omitted + omitted),
            IsTruncated = truncated || omitted > 0, FailureType = batch.FailureType == null ? null : Display(batch.FailureType, 128) };
    }
}
