# PHYS8.2 — attribution и уменьшение allocations

Завершён 16.11.2026, версия `1.2611.16.3-alpha`. [Задача](PLAN.md#phys82),
[команды/результаты](HISTORY.md#phys82-2026-11-16), [source/package/report SHA и числа](PHYS82_RESULTS.json).
Предыдущий [PHYS8.1 baseline](PERFORMANCE.md) на `.2-alpha` сохранён.

## Что действительно выделяло память

GCAllocationTick trace старой published сборки показал повторяющиеся allocations:

- `PhysicsBodyDescription` и `Candidate` из `PhysicsSceneSubsystem.Capture` на каждое тело/tick;
- `KeyValuePair<EntityHandle, Entry>[]` из `Reconcile` (`_entries.ToArray()`);
- captured display class для initial-only lookup внутри query callback;
- `SimpleCommand`, `ValueCommand<PhysicsContact>`, targets и validation dictionaries
  при записи/playback contact entities.

Trace собран локальным `dotnet-trace --profile gc-verbose` и разобран TraceEvent.
GCAllocationTick — sampled weights, включая setup/warmup: эти числа нельзя считать
точными per-type byte totals или timing baseline. Точный вклад участков проверен
отдельными primary-thread GC counters до/после систем/фаз и внутри native public Step.
Probe-only marker systems не читают/меняют компоненты. Inclusive physics system и
exclusive bridge записаны отдельно, без вычитания разных percentiles.

Один и тот же новый probe восстановлен против immutable `.2-alpha` и `.3-alpha`
package cohorts в разных пустых cache; SDK/TFM/LangVersion/features, exact package
closure, архивы и output DLL SHA проверены. Таким образом comparison не смешивает
изменение consumer с изменением production. Для normal baseline сохраняются прежние
workloads/120warmup/600samples. Attribution timings не сравниваются с baseline.

## Изменения реализации

`PhysicsSceneSubsystem` владеет bounded Candidate scratch pool. Каждый capture
сбрасывает **все** значения и ссылки перед новым полным обходом; размер ограничен
числом допущенных bodies/shapes. Scratch не является опубликованным состоянием и
не позволяет пропускать validation неизменившихся entities. Delete/replacement и
перестановка packed query order не привязывают запись к прошлой entity generation.
При завершённом Dispose коллекции освобождают ссылки; failed native cleanup сохраняет
прежний retry owner. DTO pool не владеет ни заимствованным World, ни native ресурсами.

Entry хранит immutable body description, полученный при создании actual body.
Новый overload `PhysicsValidation.ValidateBody(options, body, shape, pose, linear, angular)`
проверяет настройки и **текущий** motion snapshot без нового descriptor object.
Оба overload проходят один набор проверок options/ID/motion/shape/mass/material/
filter/speeds. Старый API сохраняет поведение/error priority. Неизменяемые character
options переиспользуются только при совпадении descriptor; изменённые настройки
проходят capture/validation заново. Input/state/sequence проверяются каждый tick.

`Reconcile` сначала собирает removals в reusable список, затем меняет dictionaries;
снимок `_entries.ToArray` каждый tick не нужен. Min/max dynamic mass вычисляется
одним полным проходом без LINQ/промежуточного массива. Authored-root lookup вынесен
из hot query callback, устранив вложенный capture closure на каждое тело.

Full candidate preflight по-прежнему завершается до native mutation/Step и ECS
writebacks. Root/unit-scale/ExternalMotion, velocity, shape, complete/orphan roles,
mass ratio, teleport/jump sequences и output ownership не ослаблены. Контактные
entities по-прежнему создаются/удаляются deferred-командами с прежними generations
и next-tick Gameplay timing. Общий EntityCommandBuffer/allocator не менялся.

## Результат на том же Ryzen9 9900X / Windows x64 / .NET11

Primary-thread allocations на tick, exact before/after consumer, обе DI:

| Workload, full ECS tick | `.2-alpha`, Б/tick | `.3-alpha`, Б/tick | Снижение |
| --- | --- | --- | --- |
| 1000active bodies | 444653–444765 | 440–842 | более99.8% |
| 1000sleeping bodies | 444653.44 | 440 | 99.90% |
| 256walking controllers | 334266.08 | 209719–209732 | около37.26% |

Изолированный расклад1000sleeping bodies, Microsoft DI (Cobalt даёт те же значения):

| Участок | До, Б/tick | После, Б/tick |
| --- | --- | --- |
| Physics bridge, исключая native public Step | 444261.44 | 48 |
| Native public Step | 0 | 0 |
| Builtin movement | 96 | 96 |
| Movement phase playback | 0 | 0 |
| Transform system | 88 | 88 |
| Extraction / пустой frame snapshot | 208 | 208 |
| Итого primary thread | 444653.44 | 440 |

Active workload допускает небольшую разницу runtime/tiered-JIT allocations.
All-thread и primary-thread counters хранятся отдельно. Старый hot per-body churn
не вернулся и в normal source/fresh Release baseline; показатель больше не растёт
линейно с количеством стабильных rigid bodies. Сравнительное timing SLA не задано:
fixed-order/warmup bias PHYS8.1 не устранён, временные результаты остаются наблюдениями.

В256-controller workload сохраняются около112.7КБ/tick на запись contact commands
в physics system, около96.6КБ на их playback и56–68Б в native Step, плюс малые
постоянные участки. After trace подтверждает оставшиеся command/validation types;
бывшие per-frame Candidate/description/entry snapshots перестали быть hot источниками.
Не всё, видимое в trace, является per-frame: большие contact/pool arrays создаются
при setup и не были целью этой оптимизации.

## Проверки и границы

- 31новый xUnit case +124существующих: **155passed каждый Debug/Release**,0failed/0skipped;
  allocation guard измеряет warmed128-body graph без assertions/await/forcedGC в окне,
  отдельно подтверждая движение, world-transform tick и cleanup. Exact test —
  `WarmedTick_ReusesScratchWithoutPerBodyAllocations`.
- Cache/shape/character changes, deletion/generation reuse, invalid later entity,
  mass ratio exact/adjacent boundary и полный supplied-motion validator проверены
  `CandidateReuse_ClearsCharacterRolesAcrossDeletionAndGenerationReuse`,
  `CachedCharacterOptions_ChangedLimitIsValidatedBeforeAnyWriteback`,
  `CachedBodySettings_ChangedDescriptorFailsBeforeNativeReplacement`,
  `CachedMassEnvelope_UsesInclusiveRatioBeforeReplacement` и `BodyValidation_*`.
- Solution build Debug/Release0errors. Neutral contracts131×3, provider regression309×3,
  independent ECS52×4 прошли. Последняя ECS приёмка проверяет3license/provenance
  entries provider package по SHA.
- Macro validation:4прежних профиля/80fixtures +before/after attribution12×2;
 104fixtures/62400measured Steps. Оба DI, standalone/full ECS,14пакетов новой версии
  и14immutable старой, разные fresh caches; финальные owners/pools0.

Версия повышена один раз до финальных проверок. SDK/TFM/dependencies/native binaries
прежние, native rebuild не требовался. Полные16test suites, GPU/visual/interpolation,
other OS/AOT, expanded limits/worker profiles и общий PHYS8 release scope не заявлены.
В статическом pairing analyzer осталась prerequisite ошибка Microsoft.CodeAnalysis4.14;
его выводы не используются как test evidence. Functional tests и реальные deliveries
от этой утилиты не зависят.

Следующая измеренная цель — shared ECS command recording/playback. Её оптимизация
должна сохранить атомарный preflight, независимые published reservation maps,
batch-token/generation invalidation и освобождение ссылок на private component types
при reset/reload. Без такого контракта pooling generic commands может удержать
retired ALC. Этот срез сохранил данные lifetime и семантику событий.

Продолжение16.11.2026: [PHYS8.3](DEFERRED_COMMAND_ALLOCATIONS.md) выполнил следующую
измеренную цель shared command buffer; исходные PHYS8.2 числа/manifest выше сохранены.
