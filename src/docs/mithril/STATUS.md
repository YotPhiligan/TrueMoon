# Mithril: состояние и точка продолжения

Дата: **2026-10-12 — MIT3 bounded restart**.
[Контракт](PLAN.md#mit3-bounded-restart-контракт--2026-10-12),
[история и команды](HISTORY.md#mit3-bounded-restart--2026-10-12),
[общий статус](../STATUS.md).

MIT3 проверен на Windows x64/net10.0/SDK10.0.401 для owned child process handles
и controller lifetime boundary. **Mithril Debug89/89 и Release89/89**,
Failed0/Skipped0, новые24cases к прежним65. Restart matrix **40/40**
Debug/published Release, `Completed=true`.
[Runner](../../ManualTests/MithrilRestartProbe/README.md).

## Реализация и текущие проверки

| Область | Реализация | Фактическое свидетельство2026-10-12 |
| --- | --- | --- |
| Policies | Never/Once/OnCrash/Always; callback только terminal | Normal/nonzero exit каждой policy, mixed crash→normal, exact generation/attempt/callback counts |
| Budget / delay | RestartLimit default3, zero disables; RestartDelay default250ms; validation before launch | Explicit/default/zero budgets, explicit/default delays,4negative guard cases в handle/controller |
| Process ownership | Fresh Process/launch info/control endpoint; old generation released | Actual PID/unit/parent/endpoint identity, distinct generations, OS exit codes0/23, current Process=null |
| Automatic failure | Consumed attempt, LastRestartError/diagnostics, terminal completion | Missing executable Win32Exception, attempts1/callback1; initial-start cause guards сохраняются |
| Stop / disposal | Stop fence, cancel/drain monitor, graceful/kill/reap owned current process | Stop/Dispose pending60s delay до3s; cancelled preparation без второго child; stopped restarted child, concurrent/repeated Stop и reentrant terminal Dispose |
| Controlling lifetime | Intermediate exit сохраняет App lifetime; terminal отменяет один раз | Controller с actual owned child sequence и наблюдаемым lifetime; это component composition, без App builder/DI |
| App regression | MIT1/MIT2 App/main/child/owned external/rollback/parent lifecycle | Fresh28/28 на Cobalt/Microsoft DI в Never scope; обе configurations |
| Handle regression | MIT0 ownership и legacy stdin compatibility | Fresh16/16, включая sentinel/timeout/cancel/disposal; прежние14cases включены в suite89 |
| Enerit regression | Own-process launcher в pipes fixture | Runtime71/71 Debug и Release; generator/manual IPC matrix не повторены |
| Full solution | Non-incremental Debug build | Exit0/errors0/warnings1220; другие module suites/UI не запущены |

Все matrices имеют fresh case directories/logs и source fingerprints. Это source
ProjectReference consumers, не NuGet proof. Предыдущие MIT0/MIT1/MIT2 runs и их
ошибки сохранены в HISTORY; они не считаются текущими проверками.

## Совместимость и ограничения

- Always теперь bounded: default3 automatic attempts, delay250ms. Budget на
  explicit Start, successful restart его не сбрасывает; zero disables restart.
  Once дополнительно ограничен одним attempt. Nonzero exit — определение OnCrash.
- OnExit теперь terminal-only, intermediate process exits не отменяют App.
  OnProcessExited hook наблюдает каждую generation; in-flight terminal callback
  может завершиться после Stop fence. Intentional Stop новых callback не создаёт.
- Protected Restart() заменён RestartAsync(CancellationToken). Custom overrides
  должны соблюдать cancellation/ownership. Completion/RestartCount/
  LastRestartError доступны для наблюдения; diagnostics failure не блокирует cleanup.
- MIT3 matrix проверяет child handle/controller boundary. App/DI regression28
  использует Never: сквозной App/Enerit/restart сценарий ещё нужен в MIT4/ENE4.
  Main local services не получают process restart; External uses same process
  base, но external executable restart отдельно не запускался.
- Main общий App graph, External launched/owned без attach, app-local AddUnit,
  async child control pipe и остальные MIT1/MIT2 ограничения сохраняются.
- Другие OS, package delivery, arbitrary custom callbacks/hooks, долгий stress
  и resource trend не проверены. Passing cleanup не является zero-leak proof.

## Следующий конкретный шаг

**MIT4/ENE4**: App/AddUnit own child выполняет generated typed Enerit IPC;
normal/crash/restart/cancellation/shutdown согласуют process и transport cleanup.
На Cobalt/DI проверить восстановление канала после restart и окончательное
завершение App. [Отдельный Enerit контракт](../enerit/STATUS.md).
