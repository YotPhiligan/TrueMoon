# Mithril: состояние и точка продолжения

Последняя работа — **2026-10-28, сверка diff и планов, без новых запусков**.
В рабочем дереве меняются только composition/restart probes и их regression tests;
Mithril runtime/API, restart policies и process ownership прежние.

Ready-file follow-up исправлен27октября: reader повторяет missing-file и Windows
sharing/lock conflicts в едином8s deadline с cancellation у IO/delay. Публикация
temp+Move и identity/reap assertions сохранены; malformed/null JSON даёт ошибку.
Directed RED2→GREEN и12новых cases подтверждены сохранёнными artifacts.
[Причина и точные команды](HISTORY.md#ready-json-bounded-read-retry--2026-10-27).
Накопленные предыдущие сводки — в [HISTORY](HISTORY.md#сверка-рабочего-дерева-и-архив-status--2026-10-28).

## Реализация и фактические проверки

Последние module/fixture runs — **2026-10-27**, Windows x64/net10.0,
SDK10.0.403/xUnit2.9.3/VSTest. Это сохранённые результаты, не запуск этой сверки.

| Проверка | Фактический результат | Evidence |
| --- | --- | --- |
| Полный Mithril Debug/Release | 111/111 в каждой конфигурации, failed/skipped0 | [Debug TRX](../../TestResults/MithrilReady-20261027-1/full-Debug/mithril.trx), [Release TRX](../../TestResults/MithrilReady-20261027-1/full-Release/mithril.trx) |
| Ready-file regression | RED2sharing failures → GREEN12cases | [Контракт/evidence](HISTORY.md#ready-json-bounded-read-retry--2026-10-27) |
| Fresh published Release | Composition14/14 на Cobalt/DI иrestart20/20; Completed=true, source changes0 | [Summary](../../TestResults/MithrilReady-20261027-1/published-summary.json) |
| Связанный nonincremental solution Debug build | 0errors/1541warnings | [Log](../../TestResults/MithrilReady-20261027-1/solution-Debug.log); это build, не full solution tests |

Исторический MIT5 diagnostics/package proof от15октября: Mithril94/94 и
Enerit126/126 Debug/Release, restart40/lifecycle18 и package108/108. Он сохраняет
собственные дату и scope, [команды и limitations](HISTORY.md#mit5-operational-diagnostics--2026-10-15).
Fresh NuGet/полный MIT6/performance этим fixture fix не повторены.

## Ограничения

MIT5 выполнен в bounded Windows x64/net10.0 same-build/cooperative own-child scope.
Main использует общий App graph и не получает process trace. External остаётся
launched/owned без borrowed attach; failed external launch проверен, независимый
external restart не подтверждён. Default Always bounded3/delay250 и terminal-only
OnExit/stop fence сохраняются. Started подтверждает OS start, не readiness.

Diagnostic observer синхронный; sink errors изолированы, blocking/reentrancy не
обещаются. Concurrent Stop/monitor notifications могут перемежаться; Terminal —
решение, не callback acknowledgement. Repeated Stop может дать NoProcess; reentrant
terminal Dispose даёт TerminalCallback и пропускает self-wait. Error сохраняет
исходный Exception и может содержать OS details. StopFailed реализован, реальные
OS permission/reap failures не инъецировались. Это не durable audit/recovery API.

Other OS/compiler/RID/self-contained/single-file/trimming/NativeAOT, arbitrary
hooks/transports, uncooperative services и long resource trend не подтверждены.
Parent abrupt-exit proof относится к child/server cleanup, не workers/sink убитого parent.

## Следующий конкретный шаг

Ready-file quality follow-up закрыт. Legacy Aether Build/Replace fixtures и
compiler follow-ups также закрыты отдельными engine proofs. Следующий доступный
общий шаг — ARCH2e.1/2 independent NuGet build/publish/headless/App/DI/resources;
GUI/native acceptance остаётся открытой и GPU runs отложены после BSOD.
Следующий общий шаг ведётся в [engine STATUS](../engine/STATUS.md#следующий-конкретный-шаг).
Использовать проверенный первый MIT6a профиль при preview integration;
ARCH1b.1 и ED1a уже выполнены в своих границах. Полный fault matrix,
real OS reap/permission failures и MIT6c containment/trends остаются отдельными.
Не дублировать Aether Hello/heartbeat/recovery в Mithril и не включать второй
автоматический restart поверх Aether replacement. Graceful delivery fix выполнен
как регрессия ENG4e; у MIT6 проверен только первый load-stop профиль, остальные
предложения не расширяют текущие гарантии автоматически.
UI physical DPI/real loss/VRAM и внешний K7 сохраняются в [локальном статусе](../alloy/STATUS.md);
bounded engine consumers имеют собственные проверки в engine STATUS.
Cobalt allocations и Enerit scalar CPU/allocation profile остаются отдельным
измерительным backlog, не новыми process capabilities.

Предыдущие MIT0–MIT4 и package runs2026-10-14 сохранены в [HISTORY](HISTORY.md).
