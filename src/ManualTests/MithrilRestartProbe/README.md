# Mithril restart probe

MIT3 source ProjectReference consumer: Windows x64/net10.0, owned child handles,
App lifetime. Не проверяет NuGet delivery, другие OS или общий App/Enerit IPC.

Из `src`:

```powershell
pwsh -NoProfile -File ManualTests/MithrilRestartProbe/RunRestartSmoke.ps1
```

Runner строит Debug и публикует framework-dependent Release без RID, выполняет
20 сценариев в каждой конфигурации (40 cases). У каждого случая новый каталог;
logs/JSON/SHA256 и environment находятся в ignored `TestResults/MithrilRestart/manual`.
`Completed=true` появляется после всей матрицы. Все процессы создаёт probe;
timeouts завершают только его owned tree.

| Сценарии | Независимое наблюдение |
| --- | --- |
| never-normal/crash | Один PID и terminal callback, restart0 |
| once-normal/crash | Два PID, один automatic attempt независимо от exit code |
| oncrash-normal/mixed/budget | Normal exit завершает unit; crash перезапускает до normal exit или бюджета |
| always-normal/crash | Любой exit перезапускается до заданного бюджета |
| limit-zero / default-budget-delay | Ноль disables restart; defaults дают3 attempts и250ms delay |
| delay | Между exit observation и следующей подготовкой выдержана настройка100ms (timer tolerance5ms) |
| stop-delay / dispose-delay | Задержка60s отменена; completion/cleanup до3s, следующего launch нет |
| stop-preparation | Token отменяет pending launch preparation, второго readiness/PID нет |
| stop-restarted / concurrent-stop | Новый child завершается gracefully с exit0, repeated/concurrent Stop не создаёт restart |
| restart-failure | Missing executable даёт Win32Exception в LastRestartError, один consumed attempt, один terminal callback |
| controller-lifetime | Первый exit не отменяет App; после второго terminal exit lifetime отменяется один раз |
| reentrant-terminal-dispose | Exit callback может Dispose собственный handle без ожидания самого monitor |

Child публикует readiness с PID/unit/parent/control endpoint и ждёт отдельного
release signal или qe в async control pipe. Exit codes сравниваются с заранее
заданными0/23; прежние OS handles удерживаются observers до exit. Новые PID и
control endpoint проверяются для каждой generation. После sequence проверяются
monitor completion, отсутствие current Process и повторный Stop. Report materializes
exit codes до освобождения observers; deferred enumeration не используется.

Ready reader повторяет только missing-file и Windows sharing/lock conflicts в
едином8s deadline, включающем IO и retry delay; malformed/null JSON остаётся ошибкой.
Directed режим `--read-ready <directory> <cancelAfterMilliseconds>` читает readiness
generation1 для ReadyFileProcessTests: `RETRY` подтверждает transient read failure,
0сохраняет обычный budget, положительное значение задаёт дополнительную раннюю отмену.

`RestartLimit` — nonnegative maximum automatic attempts на один explicit Start
(default3, initial launch не считается); не сбрасывается успешным restart.
`RestartDelay` — nonnegative milliseconds до каждой automatic attempt (default250).
Once дополнительно ограничен одним attempt. Failed automatic launch расходует
attempt и завершает sequence; бесконечного retry не происходит. OnCrash означает
exit code !=0, а не анализ причины OS exit. Always теперь bounded даже по умолчанию.

OnExit callback относится к terminal unit exit после policy/budget/failure;
intermediate generation exit и intentional Stop/Dispose callback не дают.
`Completion`, `RestartCount`, `LastRestartError` доступны для наблюдения.
Protected hook `Restart()` заменён на `RestartAsync(CancellationToken)`; custom
handle overrides должны поддерживать отмену и новый owned process. Exit hooks
и diagnostics не являются общей гарантией произвольной пользовательской reentrancy.

MIT5: report содержит `Diagnostics` — структурированные snapshots категории
`UnitProcess`, полученные через настоящий `EventsSource<UnitsController>` и
`DiagnosticSubscription`. RunId/generation/PID связывают launch/exit/restart/release;
effective policy/limit/delay и terminal reason различают policy, budget и failure.
ErrorType в JSON — безопасная проекция; исходный Exception в payload проверяется
по reference identity. Runner сверяет counts/PID/exit и source SHA в конце.
Детальный порядок и поля проверяет существующий xUnit `RestartProcessTests`.
Используйте отдельный `-ResultsDirectory` для каждого сохранённого запуска.
[Подключение и ограничения](../../docs/mithril/DIAGNOSTICS.md).

[Контракт](../../docs/mithril/PLAN.md), [статус](../../docs/mithril/STATUS.md),
[история и команды](../../docs/mithril/HISTORY.md).
