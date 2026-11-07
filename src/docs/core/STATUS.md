# Core: состояние и точка продолжения

**Сверка 2026-11-05:** документация и межмодульная очередь; код и проверки не менялись.
[План](PLAN.md#current-core-queue), [прежняя сводка целиком](HISTORY.md#status-before-planning-revision-2026-11-05).

## Реализация и проверки

App/bootstrap/configuration, cooperative lifecycle/rollback и owned/borrowed
disposal реализованы в своих bounded scopes. CORE6 частичен: structured events,
early filters/bounded pipeline, console/rotating JSONL/memory sinks, operations,
health/read windows и borrowed history/import/archive/configuration readers есть.
App остаётся owner; reader aliases не создают новых owners или sink flush.

Последняя собственная указанная проверка — **2026-10-30: Core398/398 Debug/Release**,
failed/skipped0, narrow56/56. [Archive/configuration evidence](HISTORY.md#архив-jsonl-и-конфигурация-для-ed8a--2026-10-30).
Предыдущие file-worker/borrowed-health/headless/package proofs сохраняют даты
и условия в HISTORY. Editor console/report/child health уже имеют scoped consumers;
ED8a.2 больше не назначается будущим prerequisite Core.

## Ограничения

Cooperative shutdown не прерывает ignoring-token Task. Configuration persistence
не обещает power-loss durability, concurrent atomic create или arbitrary
external-writer protection. DI adapters сохраняют provider-specific disposal.
App-private diagnostics routing, bootstrap-before-pipeline, общий artifact/report
quota и full aggregate metrics не выводятся из текущих JSONL/readers contracts.
CORE6e generation и performance — отдельные задачи после конкретного consumer.
Other OS/RID/AOT/долгие trends и общая release matrix не подтверждены.

## Следующий конкретный шаг

Текущий Editor не требует нового Core API для group reset/camera. Для reliability
предоставлять evidence по [повторному post-reap drain](../engine/PLAN.md#arch2e4b-drain-diagnostics);
не менять ProcessOutputCapture/ThreadPool/timeouts без подтверждённой причины.
Для собственного развития сначала выбрать consumer и контракт App-private/bootstrap
routing либо aggregate metrics CORE6d; затем implementation и собственные проверки.
Типизированный generator не является prerequisite прикладного Editor.
Общий порядок — [roadmap](../PLAN.md#current-roadmap).
