# Mithril: состояние и точка продолжения

**2026-11-05 — ревизия планов без runtime/API изменений и новых запусков.**
[План](PLAN.md#current-mithril-queue), [прежняя сводка](HISTORY.md#status-before-planning-revision-2026-11-05).

## Реализация и фактические проверки

Owned main/child/external process lifecycle, bounded restart и operational
diagnostics реализованы в same-build/cooperative scope. Started — OS start,
не readiness. Aether владеет своим Hello/heartbeat/replacement/recovery;
второй автоматический restart в Mithril не назначен.

Последняя собственная recorded проверка — **2026-10-27**:111/111 Debug/Release,
ready-file RED2→GREEN12, fresh published composition14/14 иrestart20/20.
Reader повторяет missing-file/Windows sharing/lock conflicts в едином8s deadline;
malformed/null JSON остаётся ошибкой. [Evidence](HISTORY.md#ready-json-bounded-read-retry--2026-10-27).
Последующий ARCH2 package/headless/native consumer выполнен у Aether; это не
новая полная Mithril или MIT6 acceptance matrix.

## Ограничения

External launched/owned без borrowed attach; independent external restart не
подтверждён. Diagnostic observer синхронный, errors изолированы, blocking/reentrancy
не обещаются. Concurrent stop/monitor callbacks и repeated Stop сохраняют
прежние Terminal/NoProcess semantics; Error может содержать OS details.
Real OS permission/reap failures, uncooperative services, containment/trends MIT6c,
other OS/RID/AOT и durable recovery не подтверждены. Parent abrupt-exit proof
не наблюдает workers/sinks убитого parent. Остальные детали — в архиве/HISTORY.

## Следующий конкретный шаг

**Оставшийся MIT6 fault/containment/resource-trend профиль** по выбранному consumer;
перед реализацией определить unsupported/uncooperative поведение и ownership.
Ready-file fix и ARCH2 headless/native не ставятся повторно как незавершённые функции.
Post-reap output drain расследуется у [Aether/Core](../engine/PLAN.md#arch2e4b-drain-diagnostics),
пока нет доказанного Mithril defect. Hardware K7/K8 остаётся у UI.
Прикладной порядок — [общий roadmap](../PLAN.md#current-roadmap).
