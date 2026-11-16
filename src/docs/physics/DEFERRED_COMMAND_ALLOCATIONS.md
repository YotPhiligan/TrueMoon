# PHYS8.3 — allocations deferred-команд контактов

16.11.2026, версия `1.2611.16.4-alpha`. [Задача](PLAN.md#phys83),
[команды и проверки](HISTORY.md#phys83-2026-11-16), [manifest/SHA](PHYS83_RESULTS.json).
Продолжение [PHYS8.2](ALLOCATION_OPTIMIZATION.md): меняется общий Runtime ECS buffer,
используемый physics bridge для next-tick contact entities. Публичные API, форматы,
лимиты количества команд, порядок систем и семантика контактов сохраняются.

## Изменение хранения и lifetime

Раньше каждая Create/Destroy и generic Add/Set/Remove создавала отдельный объект
команды. Playback создавал targets array, список reservations и validation state
с новыми dictionaries. Теперь buffer владеет списком value records, повторно
используемыми targets/reservations/validation и блоками скопированных payload bytes.
Обычный блок растёт до16KiB; отдельный более крупный компонент помещается целиком
в больший блок. Нет единого общего byte counter/Array.MaxLength для всего batch.
Add/Set копируют reference-free `T` при записи через MemoryMarshal; Remove не
записывает payload. Выравнивание и завершающие байты проверяются отдельно.

Типизированные обработчики остаются instance-owned: stable types кешируются в
пределах существующего command budget. Исчерпание кеша не запрещает запись нового
легального типа. Для collectible types, включая closed generic из shared assembly,
проверяется `Type.IsCollectible`; такие обработчики удаляются после успешного batch.
Статического кеша generic-команд или приватных Type нет.

Полный preflight по актуальному World всегда предшествует commit. Reused validation
заново читает allocator/free stack и очищает component Type references в `finally`
при успехе, отмене и ошибке. До commit отдельно создаётся независимый
`CommandPlaybackResult`: массив опубликованных handles никогда не выдаётся из
повторно используемого scratch. Старые mappings не меняются после нового batch,
Reset, роста buffer или Dispose.

Отмена оставляет pending values/handlers для retry; ошибка validation требует
Reset и не меняет World. Неожиданная ошибка commit сохраняет AppliedCommandCount
и fault World без rollback. Reset очищает значения и все обработчики, успешный
playback — значения и collectible обработчики. Scratch ёмкости сохраняются для
повторного использования; Dispose освобождает их. Заимствованный World и values
не становятся owned ресурсами buffer.

## Измерение

Тот же probe/quality/workloads и120warmup+600samples, что в PHYS8.2. Один consumer
восстановлен против immutable `.3-alpha` и `.4-alpha` package cohorts в отдельных
пустых caches. SDK11.0.100/net11.0/runtime-async, exact graph/archive/output SHA
проверены. Primary-thread counters измеряют весь tick и отдельные фазы; attribution
timings не сравниваются с baseline. Это Windows x64, один worker; timing SLA не задан.

В сценарии256walking controllers с контактами на обоих DI:

| Участок, Б/tick | До (`.3-alpha`) | После (`.4-alpha`) |
|---|---:|---:|
| Physics bridge, без native Step |112693–112694|48|
| Movement-phase playback |96577.7|6224|
| Полный ECS tick |209719–209732|6720|

Снижение полного tick примерно96.8%. Оставшиеся6224Б — detached result и собственный
массив256reservation handles; эту allocation сохраняет контракт независимых mappings.
В остатке tick также native Step56Б, movement96Б, transforms88Б и extraction208Б.
Запись прогретого набора stable types, помещающегося в кеш, не создаёт объект на
каждую команду. Рост scratch, типы вне bounded кеша и collectible обработчики могут
выделять память; обещания zero-allocation для произвольного batch нет.

## Контрактные проверки

`EntityCommandBufferAllocationTests` проверяет copied/unaligned/mixed values,
несколько payload-блоков и60KB компонент, reuse/Reset, исчерпание command budget,
устойчивость опубликованных mappings, cancellation retry с изменившимся World,
полный preflight и injected commit failure. Collectible ALC реально собираются
при живых World/buffer после Remove playback либо Reset/Dispose; pending command
удерживает свой тип до retry/discard. Пулы уже добавленных private components
по-прежнему принадлежат World — их lifetime не подменяется buffer cleanup.

Ключевые tests:

- `SegmentedPayloads_CopyAcrossBlocksAndReuseOversizedValuesAfterReset`;
- `PublishedMaps_RemainIndependentAcrossGrowthReuseResetAndDispose`;
- `WarmedContactLikeBatches_AllocateOnlyBoundedDetachedMappings`;
- `CollectibleHandlers_AreReleasedAfterBatchResetOrDispose`;
- `PendingCollectibleRemoval_RetainsTypeUntilPlaybackThenReleases`.

`PhysicsContactAllocationTests.WarmedContactProjection_BoundsAllocationsAndPreservesEachPair`
проверяет реальные32sensor/floor contacts, Stay/tick/body/entity mappings и retirement
старых event entities, bounded warmed allocations и cleanup на Cobalt/MS DI.
Mid-preflight cancellation после заполнения scratch не вызывается через специальный
test hook; cleanup на этом пути подтверждён общим `finally`, а retry проверен отдельно.

Финальные counts, команды, source/package/report hashes — в manifest и HISTORY.
Общий PHYS8 остаётся открыт: expanded capacities/workers, raw callbacks/stalls,
visual/interpolation, other OS/AOT и physical/release acceptance самостоятельны.
Native sources/binaries, ABI, зависимости, SDK и TFM не изменялись; native rebuild
для этого среза не требуется.
