# TrueMoon: общий статус

**2026-11-17 — ARCH1g.1c: permanent model subasset IDs и atomic ledger publication.**
Version **1.2611.17.4-alpha**. [Контракт/границы](engine/MODEL_SUBASSET_LEDGER.md),
[задача](engine/PLAN.md#arch1g1c-subasset-ledger), [проверки/SHA](engine/MODEL_SUBASSET_LEDGER_RESULTS.json).
Семантическое сопоставление, rename/tombstones/explicit restore, App/IFactory/session,
readonly Open, generation CAS и единый checksummed CPU+ledger sidecar проверены.
Native parser изолирован; источником permanent ID не становятся порядок или content hash.

Текущие Aether3698/3698 и Editor1507/1507 D/R tests, failed/skipped0;
74new cases,10profiles/100groups/1425assertions;400owned processes reaped,
4source→fresh ledger handoffs,11matching local packages, exact native/loaded DLL SHA.
Результаты относятся к bounded static CPU profile; all-solution/GPU/release acceptance
не объявляется. Delivery/worker/feed сохранены, пакеты не опубликованы.

**g.1 CPU producer завершён; далее g.2 Nom/build/runtime delivery и activation.**
Producer-local authoring sidecar не заменяет Nom catalog/CAS. PHYS8 physical/DPI/device
loss, ED/AUD, other OS/AOT и release gates самостоятельны. Historical diagnostic
drain cause открыт; чужие UIQA-DOC изменения сохранены.
[Предыдущая сводка](HISTORY.md#common-status-before-g1c-2026-11-17).
