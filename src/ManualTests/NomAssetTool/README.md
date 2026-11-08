# NomAssetTool

Headless consumer публичных `TrueMoon.Nom` и `TrueMoon.App`. Это отдельное console
приложение для frozen asset catalog → backup → restored catalog → explicit GC.
Оно не зависит от Aether, UI, importer, renderer, native decoder или internal Nom API.

Source project использует ProjectReference для обычной workspace сборки. NOM7 delivery
runner копирует эти C# files в свежий внешний project с PackageReference на упакованные
Nom/App, отдельными feed/cache и без ProjectReference/friend assembly. AssemblyName
`NomAssetTool` отличается от Nom test/probe friends.

CLI принимает только полностью заданные local paths. Каждый invocation требует
новый `--report` файл с существующим parent. Команды исполняются отдельными процессами:

```text
create ROOT PROJECT_GUID STORE_GUID RECEIPT_JSON --report REPORT_JSON
verify ROOT PROJECT_GUID STORE_GUID RECEIPT_JSON --report REPORT_JSON
export ROOT PROJECT_GUID STORE_GUID RECEIPT_JSON BACKUP_ROOT --report REPORT_JSON
restore BACKUP_ROOT RESTORED_ROOT PROJECT_GUID STORE_GUID RECEIPT_JSON --report REPORT_JSON
collect ROOT PROJECT_GUID STORE_GUID RECEIPT_JSON --report REPORT_JSON
verify-current ROOT PROJECT_GUID STORE_GUID RECEIPT_JSON --report REPORT_JSON
```

`create` требует absent ROOT/RECEIPT. Он публикует opaque shader и geometry v1
(2MiB+17bytes), фиксирует manifest с exact dependency и durable pin, затем публикует
geometry v2 (64KiB+17bytes). Оба старых operation tickets явно истекают; старые bytes
удерживаются snapshot. Named receipt schema1 хранит persistent IDs, exact versions,
tickets, pin/manifest и generation. Expected bytes определены независимо в consumer.

`verify` проверяет reopened heads, ordinal source/kind/tag pages, forward/reverse exact
dependencies, committed current outcome/expired old tickets, durable pin и old snapshot
bytes. `export` сначала выполняет ту же проверку и сохраняет immutable backup.
`restore` использует отдельную typed factory и тот же receipt без source root.

`collect` явно unpin/remove manifest, expires current outcome, resumes durable cleanup,
проверяет dry-run, исполняет bounded GC slices и compaction. Только old geometry
должна стать недоступной; shader/current geometry/generation3 сохраняются. Повтор
`collect` безопасен для уже collected catalog. `verify-current` проверяет этот state
после следующего process reopen. Receipt/backup не перезаписываются и не удаляются.

`AssetTransferWorkflow` создаётся через App registration factory с constructor-injected
`IFactory<IProjectAssetStore, OpenAssetStoreArguments>` и restore factory. Контейнер
доступен только composition root в Main. Workflow владеет products, snapshot leases
и read streams, borrowed dependencies не освобождает. Все products закрываются до App;
report проверяет registry0 и actual App release. Ошибки не запускают recursive cleanup.

JSON report содержит Completed, ScenarioName, exact Checks, Consumer, actual Nom module
path/SHA, ProjectId/StoreId/Generation и Failure. Source/published/NuGet proof results
публикуются в Nom STATUS/HISTORY родительским runner; наличие этого проекта не является
passing результатом. Physical disk-full/power-loss, fresh RID matrix и release gates
проверяются отдельно.
