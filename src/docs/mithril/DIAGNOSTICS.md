# Диагностика owned processes

MIT5, контракт 2026-10-15. [План](PLAN.md), [статус](STATUS.md),
[результаты и команды](HISTORY.md#mit5-operational-diagnostics--2026-10-15).

В настройке App подключите существующую диагностику TrueMoon. При использовании
`AddUnit` источником process-событий служит `TrueMoon.Mithril.UnitsController`;
default filter `TrueMoon` в `UseDiagnostics` его включает. Direct handle использует
переданный ему `IEventsSource`; для custom имени нужен соответствующий filter.

```csharp
using TrueMoon.Diagnostics;
using TrueMoon.Mithril;

// context — IAppConfigurationContext внутри настройки App.
context.UseDiagnostics(d => d.OnEvent(e =>
{
    if (e.Category != "UnitProcess" || e.Payload is not UnitProcessDiagnostic p) return;
    Console.WriteLine($"{e.Time:O} run={p.RunId} unit={p.UnitIndex}/{p.UnitName} " +
        $"generation={p.Generation} owner={p.ParentProcessId} pid={p.ProcessId} " +
        $"{p.Stage} attempt={p.RestartCount}/{p.RestartLimit} " +
        $"reason={p.Reason} exit={p.ExitCode} error={p.Error?.Message}");
}));
```

Сохраняйте нужные поля в своём sink. Callback синхронный: выполняйте его быстро,
не вызывайте lifecycle handle/controller из обработчика. Исключение sink изолировано,
но произвольное блокирование/reentrancy не поддерживается. Повторный `UseDiagnostics`
заменяет предыдущие настройки по Core contract. Legacy текстовые категории
`UnitProcessOnExited`, `UnitProcessRestart` и Exception events сохранены.

## Поля и переходы

`RunId` — новый Guid каждого принятого explicit Start после validation. Индекс unit
сам по себе не уникален между Apps; связывайте события по RunId. `Generation` начинается
с 1 в LaunchStarting и считает попытки, включая failed/cancelled preparation.
`ProcessId` null до OS start текущей попытки; после освобождения wrapper сохраняется
последний PID для связи событий. При новой попытке PID сбрасывается. `Restarting`
описывает решение после предыдущей generation; следующая начинается с LaunchStarting.
`RestartCount` считает consumed automatic attempts, initial start в него не входит.
`ParentProcessId` — переданный PID владельца, сохраняющийся при чтении отчёта другим процессом.

Каждый snapshot несёт effective RestartPolicy/RestartLimit/RestartDelay/TerminationDelay:
defaults Always/3/250ms/1000ms. Payload фиксируется при переходе даже для lazy sink.

| Stage | Значение / Reason |
| --- | --- |
| LaunchStarting → Started | Подготовка и успешный OS start; готовность сервиса требует отдельного handshake |
| LaunchFailed | LaunchFailure с исходным Error, Cancelled при отменённом token либо StartReturnedFalse |
| Exited → ProcessReleased | NaturalExit, exit code и PID; wrapper освобождён monitor |
| RestartScheduled → Restarting | Policy разрешила попытку в пределах бюджета; delay, затем consumed attempt; scheduling может отмениться Stop |
| Terminal | Policy запретила restart, RestartLimit исчерпан или Failure; решение завершить sequence, не подтверждение доставки callback |
| SequenceFailed | RestartFailure/MonitorFailure и исходный Error; у automatic failure тот же объект доступен в LastRestartError |
| StopRequested | Stop уже установил fence и получил lifecycle gate; дальнейший launch запрещён |
| Killing | GracefulStopIncomplete, forced termination только owned tree; успешный kill ещё не подтверждён |
| ProcessReleased | Graceful/Killed/AlreadyExited при Stop, exit code и PID; wrapper уже disposed |
| Stopped | Cleanup и monitor drained; NoProcess для повторной/пустой остановки. TerminalCallback — reentrant Dispose из terminal callback, self-wait пропущен |
| StopFailed | Cleanup не завершён, исходный Error; прежний retry/ownership contract сохраняется |

Natural Terminal и intentional Stop различаются. Concurrent Stop и завершение monitor
могут перемежать notifications; отслеживайте transitions/generation, не считайте каждый
Stopped новым освобождённым процессом. Решение RestartScheduled/Terminal может быть
наблюдено на границе отмены; оно не обещает следующий launch/terminal callback.
Stopped snapshot фиксируется до освобождения gate и доставляется после monitor wait:
конкурентный новый explicit Start не подменяет identity предыдущего cleanup.

В payload нет argv, environment values или control endpoints. Error сохраняет исходный
Exception и может содержать OS paths/details. Для JSON проецируйте Error в нужные
поля, например тип/message; универсальная сериализация Exception не обещается.
Это наблюдаемость без новых DI/logging contracts и dependencies. MainProcess,
parent observer и controller-wide aggregate errors не получают новый process protocol.
Pre-cancelled/invalid/duplicate Start до launch preparation не создаёт launch trace.

## Воспроизведение

Из `src`, PowerShell 7/.NET10, Windows x64:

```powershell
pwsh -NoProfile -File ManualTests/MithrilRestartProbe/RunRestartSmoke.ps1 -ResultsDirectory TestResults/MithrilDiagnostics/restart-fresh
pwsh -NoProfile -File ManualTests/MithrilLifecycleProbe/RunLifecycleSmoke.ps1 -ResultsDirectory TestResults/MithrilDiagnostics/lifecycle-fresh
pwsh -NoProfile -File ManualTests/UnitsPackageConsumer/RunPackageSmoke.ps1
```

Выбирайте новые output directories. Restart runner проверяет 20 scenarios через
Debug apphost и published Release, lifecycle — 9 scenarios (включая throwing sink).
Hosted `dotnet <assembly>` проверяет xUnit suite. Независимый package runner создаёт
fresh feed/cache/consumers: 108 cases Debug/Release × build/publish на Cobalt/DI и
DI без Cobalt analyzer. В 96 cases c живым parent проверяется process diagnostics;
12 abrupt-parent-exit cases подтверждают прежний child cleanup без наблюдения
убитого client/sink. Source fingerprints сравниваются в конце runner.

Это bounded same-build/cooperative proof. Other OS/RID/AOT, borrowed attach,
independent external restart, arbitrary hooks, uncooperative callbacks/services,
durable audit/recovery и long resource trend из него не следуют.
