# AST1a: runnable LiteDB prototype и проверки

> NOM2 завершён2026-11-08: [текущий backend и проверки](CATALOG.md#nom2-catalog-2026-11-08).
> Vendor runners/downloads удалены; команды и результаты этого документа исторические.

> Исторический LiteDB срез. Последующим решением2026-11-07 принята полная замена
> собственной реализацией Nom: [решение](STORAGE_DESIGN.md#nom-own-store-2026-11-07),
> [активная очередь NOM0–NOM7](PLAN.md#nom-own-queue). Результаты ниже относятся
> только к старому backend; vendor fix/upgrade/isolation больше не активная задача.

> Историческая запись до переименования в TrueMoon.Nom и разделения проектов.
> Старые counts/hashes/graph относятся к указанному запуску; текущая структура,
> повторные проверки и команда — [NOM_RENAME](NOM_RENAME.md#nom-rename-2026-11-07).
<a id="ast1a-2026-11-07"></a>
## Выполнено 2026-11-07

[Runnable consumer](../../ManualTests/TrueMoon.Nom.Probe/README.md) реализован отдельно
от Aether и production package graph. Pinned LiteDB5.0.21, actual assembly5.0.21.0;
Windows x64, SDK10.0.403/runtime10.0.14, net10.0. Source Debug и framework-dependent
published Release прошли **по10групп**. Это AST1a owner/schema/codec/transaction proof,
не реализация всего [LiteDB v1](LITEDB_DESIGN.md) и не закрытие AST1b–d/AST2.

## Повторяемый запуск

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/MyFreshNomRun
```

Финальный фактический запуск:

```powershell
& ./ManualTests/LiteDbPrototype/Verify.ps1 -EvidenceDirectory TestResults/LiteDbAst1a-20261107/FinalReviewed -ArchiveDirectory TestResults/LiteDbAst1a-20261107/no-preexisting-archives
```

Missing-archive bootstrap скачал official NuGet files в новый feed, SHA gates прошли.
Каждый run использует новый OS TEMP project/package cache и изолированную NuGet.Config.
ProjectReference — только source TrueMoon.Contracts для реального typed IFactory;
общий CPM/legacy DotNetCliToolReference изолированы invocation-only property, source
project/Directory.Build.props/Directory.Packages.props не менялись. SDK reference packs
предустановлены. Это не fresh production Assets package consumer.

## Закреплённые входы и результат

| Артефакт | Версия / SHA256 |
| --- | --- |
| LiteDB official nupkg |5.0.21 /938A4F5F9D6A28383DE8CCFCBDB8897E78432399215232CF38DBF147E03EC167 |
| System.Buffers official nupkg |4.5.1 /C30B3DD2C7E2F4CEE4B823D692FD42118309B42AB1F5007F923D329A5B0D6B12 |
| netstandard2.0/LiteDB.dll, source и publish |5.0.21.0 /AE31AC6A93549217B9E8B81497D1EE831658ADE6DF9E1EE20F2838F22DE5E218 |
| NuGet repository commit |391cc9318c5be6e56cb71b3ce14b1ed9cb324763 |

Resolved package graph ровно LiteDB/5.0.21 и System.Buffers/4.5.1. Runtime может
использовать framework implementation System.Buffers; resolved package version не
выдаётся за actual loaded framework assembly version. LiteDB archive не содержит
runtimes/native assets. MIT license/provenance взяты из nuspec; полный production
package delivery/лицензионный manifest будущего модуля относится к AST2.

FinalReviewed: **2/2profiles,5commands exit0,3unique SDK binlogs,5frozen research source
hashes, source drift0**. Debug/publish build —0errors,0warnings из нового prototype;
167warnings source Contracts — существующие XML documentation warnings. Отдельная
negative package проверка испорченного nupkg дала SHA rejection до restore/build:
CommandsStarted0, не заменила положительные profiles. Raw logs/json/binlogs — ignored
TestResults/LiteDbAst1a-20261107; эта запись сохраняет результат без зависимости от их retention.

## Что реализовано

Typed factory ограничивает число owners; Create/Open имеют разные preconditions.
Explicit caller-supplied ProjectId/StoreId сохраняются в singleton header. Root lock
удерживается до закрытия DB; библиотека открывается Direct. Read-only preflight
проверяет stored header/schema/codec/collation и native unique binding index,
включая его expression, до writable open. Reject не создаёт замену datafile.

Dedicated thread владеет DB/collection access, Begin/insert/commit/rollback и Dispose.
Factory стартует thread под registry gate, ожидает ready вне gate; stop не может
запустить поздний untracked owner. Bounded queue передаёт detached payloads, checked
generation publication атомарна для тестового metadata batch. Pre/queued cancellation
не бросает владение underlying command. Factory Dispose drains каждый live owner;
нормальные startup failure/stop/repeated Dispose закрывают DB/lock и join thread.

Explicit BSON codec имеет versioned fields, length-prefixed culture-independent
identity key, strict GUID/schema/framing/digests и defensive copies. Research records
represent assets metadata, не full production sources/revisions/dependencies schema.
Source metadata revision algorithm здесь имеет собственный `ast1a-metadata/1` profile;
он не заменяет generic asset revision/import recipe contract.

## Доказательства по поведению

| Группа | Прямые assertions |
| --- | --- |
| CodecCultureAndIntegrity | Independent key golden71d333…; en-US/tr-TR/ru-RU, ordinal identity/composite framing, input/output mutation isolation; missing/extra/incorrect schema/type/uppercase GUID/digest, truncated/trailing BSON и invalid limits отклоняются |
| CreateOpenAndIdentity | Open missing root ничего не создаёт; Create occupied root не reset; literal semicolon/equal filename, binary collation, explicit IDs и generation/content сохраняются после reopen; DTO mutation не меняет DB |
| FailedOpenPreservesCatalog | Seeded datafile SHA unchanged после wrong ProjectId/StoreId/schema/codec/negative generation/collation, missing/wrong-expression index, missing header, unknown row schema, zero/corrupt catalog; failed owner slots освобождены |
| OwnerThreadRollbackAndReopen | Partial managed failure и native binding/primary-ID violation откатывают earlier insert и generation; native code110 подтверждён; stale/overflow generation не пишет; concurrent callers проходят один owner thread; trace покрывает open/begin/insert/commit/rollback/close; reopened set строго seed+committed |
| ExclusiveRootAndDirectProcesses | Same-process и child root lock дают IOException; отдельный child raw Direct open без owner.lock также даёт IOException; existing data не меняется; после close child открывает DB |
| SerializedBudgetBoundaries | Metadata0/64KiB-1/64KiB/over, ASCII+UTF8 locator bounds; full BSON379bytes before/at/over, aggregate batch700bytes/over и row limits проверяются до queue/native writes |
| DetachedResponseBoundaries |256rows accepted/257 rejected без partial result; serialized responses1MiB-1/exact/+1, over cap не меняет catalog generation |
| QueueCancelAndStop | Count/byte admission, exact queued bytes, ordered drain, pre-cancel no admission, queued cancellation завершается после owner observation, successful later write, repeated dispose/closed rejection |
| FailedStartupAndFactoryCapacity | Before-thread и after-DB failure, persisted startup identity/reopen,4owners/5th refusal до filesystem effects, real IFactory default/wrong-argument failures, stopped factory и pre-create cancel |
| FactoryStartupStopAndDrain | Deterministic opening/stop barrier без untracked late thread/root; factory сама drains2live owners и admitted write; native Direct handles обоих доступны child после close |

Environment gate: `CatalogReparsePrecheck` не выполнен — Windows не разрешил создание
симлинка, HRESULT0x80070522/privilege not held в обоих profiles. Catalog/root/lock reparse
guards реализованы, но actual symlink rejection и handle/ancestor replacement races
не объявлены проверенными. Это не failed core proof и не доказанная filesystem sandbox.

## Уточнение реального transaction contract

В [source NuGet commit Transaction.cs](https://github.com/litedb-org/LiteDB/blob/391cc9318c5be6e56cb71b3ce14b1ed9cb324763/LiteDB/Engine/Engine/Transaction.cs)
AutoTransaction при handled exception откатывает и освобождает transaction. Поэтому
после native unique-index error `Rollback()` возвращает false: active transaction уже
нет. Прототип принимает этот путь только для проверенного INDEX_DUPLICATE_KEY110,
сохраняет оригинальный LiteException, фиксирует native-auto-rollback и сверяет generation.
Assertions дополнительно доказывают полный прежний set до close, новый успешный commit
и exact reopened set. Любой false rollback не объявляется успешным cleanup автоматически.
Искусственный managed failure после первой записи отдельно проверяет явный rollback.

Checked Begin/Commit не ослаблены. Unknown commit/I/O outcomes, durable operation
journal и retained failed-close retry — последующие AST1b/d, не обещание этого прототипа.

## Общая проверка и точка продолжения

Один solution nonincremental Debug build выполнен отдельно:0errors/1555warnings;
warnings совпадают с предыдущим baseline, новых не добавлено. Full unit suite не
запускалась; исторические Aether diagnostics cleanup timeouts не считаются исправленными.

Следующий storage шаг — [AST1b](PLAN.md#litedb-ast1): staged/CAS bytes, operation journal,
atomic candidate publication/cancel/commit/unknown outcome и crash cut points. App
registration, public factory/API/production NuGet — AST2; engine bridge — AST3.
Snapshots/pins/GC/migrations/power-loss/disk quotas/performance не прошли acceptance
потому, что не входят в AST1a implementation. Queue/metadata/query limits выше —
проверенная часть профиля, не общий heap/RSS/disk SLA.
