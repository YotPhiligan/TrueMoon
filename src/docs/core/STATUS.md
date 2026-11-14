# Core: состояние и точка продолжения

**2026-11-12 — output progress diagnostics и current baseline ARCH2e.4b.**
ProcessOutputSnapshot расширен EOF/read/file stage/UTC, bounded queue и worker
metadata; constructor/borrowing/Completion и file bounds сохранены. [API/границы](../engine/DRAIN_DIAGNOSTICS.md).
Подробная задача ведётся у engine, новых App/DI/configuration/lifecycle APIs нет.

- Core full suite **662passed/0failed/0skipped** каждый Debug/Release; новый1case.
- Адресные ProcessOutput tests Debug:30/30, включая independent file-worker proof.
- Ordinary solution D/R0errors; все11suites по8568passed/configuration.
- Один свежий Core package `0.3.0.1-alpha.arch2e4b.20261112150909`: external
  private feed/cache, общий SDK/TFM/Features/CPM isolation, Debug и Release FDD
  consumer подтвердили progress/EOF/JSON roundtrip и exact Release assembly SHA.
- Исторический drain timeout не воспроизведён; его причина и wider release/
  GPU/performance остаются открыты. ThreadPool/deadlines не настраивались.

[Команды и ограничения](HISTORY.md#arch2e4b-2026-11-12), [общий manifest](../engine/ARCH2E4B_BASELINE.json).
CORE8 завершён 11 ноября в прежнем scope; последняя согласованная16package delivery —
[CORE8.5](HISTORY.md#core85-delivery-2026-11-11). Она не выдаётся за повторённую сегодня.

## Следующий конкретный шаг

Engine [ARCH1f.4](../engine/PLAN.md#arch1f-rendering) → минимальный Player →
ARCH3.1–5. Диагностика причин повторного timeout продолжится при evidence;
[точная очередь](../engine/PLAN.md#current-engine-queue). [Предыдущий STATUS целиком](HISTORY.md#core-status-before-arch2e4b-2026-11-12).
