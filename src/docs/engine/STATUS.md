# Aether / Editor: состояние и точка продолжения

**2026-11-17 — ARCH1g.1c: persistent subasset IDs и atomic candidate+ledger готовы.**
Version **1.2611.17.4-alpha**. [Задача](PLAN.md#arch1g1c-subasset-ledger),
[API/схема/ownership](MODEL_SUBASSET_LEDGER.md), [команды/границы](HISTORY.md#arch1g1c-2026-11-17),
[manifest/SHA](MODEL_SUBASSET_LEDGER_RESULTS.json).

Authored semantic locators проходят проверенную source→native transport binding;
постоянные Mesh/Material/Slot/BindingSet/Instance IDs отделены от indices/content.
Reorder/content edit сохраняют IDs; явные rename/restore и tombstones проверены.
Неоднозначные names, включая unused objects/ancestor paths, требуют explicit labels.

Caller-owned LedgerSession, readonly Open и App-owned shared admission1/0/Stop/drain.
Один checksummed12MiB sidecar содержит полный CPU candidate+ledger+associations;
expectedGeneration CAS, OS writer lock, flush и atomic rename. Reader во время
replacement видит целый old/new record; killed publisher не оставляет torn head.
Crash orphan staging игнорируется; локальные filesystem/power-loss границы в guide.

Текущие Aether3698/3698 и Editor1507/1507 D/R tests, failed/skipped0;
74new ledger/store/session cases. 10profiles на обоих DI:100groups/1425assertions,
360reaped workers +30readers +10killed/reaped publishers; 31coherent concurrent reads,
4source→fresh durable handoffs,11matching packages; closed feed/cache/loaded SHA.
Worker/native6.0.5/SHA/precision и source freeze проверены; delivery сохранена локально.

**Ограниченный CPU g.1 завершён. Следующий g.2 — Nom/build/runtime artifacts,
captured delivery и activation.** Producer sidecar не становится вторым asset store.
GPU/Editor asset preview, прочие форматы/OS/AOT, ED/PHYS8/AUD и release gates отдельны.
Исторический diagnostic drain cause не пересмотрен; пакеты не опубликованы.
[Предыдущий статус](HISTORY.md#engine-status-before-g1c-2026-11-17).
