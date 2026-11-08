# TrueMoon.Nom: подключение через App

**NOM2,2026-11-08:** публичный App API переключён на собственный KV.
[Catalog records/recovery/limits](CATALOG.md#nom2-catalog-2026-11-08),
[фактические результаты](HISTORY.md#nom2-2026-11-08). LiteDB implementation/dependency
удалены. AST2a API ниже сохраняется; старые execution результаты — в HISTORY.

<a id="nom-app-2026-11-07"></a>
## App API — AST2a2026-11-07, backend cutover NOM2 2026-11-08

Реализован публичный `UseNom(...)` для `IAppConfigurationContext`. Подключение работает
из App.Build, App.RunAsync и App.Builder(...).Setup(...); библиотека Nom использует
только общие TrueMoon.Contracts, без зависимости от реализации App, Cobalt или Aether.
Проверки используют настоящий App со штатным Cobalt. Nom формирует собственный пакет;
[NOM7 delivery](DELIVERY.md) проверяет public App и отдельный NomAssetTool в fresh
NuGet projects без ProjectReference/friend access.

## Подключение и открытие проекта

```csharp
using TrueMoon;
using TrueMoon.Dependencies;
using TrueMoon.Nom;

await using var app = App.Build(context => context.UseNom(options =>
{
    options.MaximumOpenStores = 2;
    options.DrainTimeout = TimeSpan.FromSeconds(5);
}));
await app.StartAsync();

// Resolution belongs to this composition root. Application services receive the factory in their constructor.
var factory = app.Services.Resolve<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>()!;
var arguments = new OpenAssetStoreArguments(
    Path.GetFullPath("sample-nom-store"),
    Guid.Parse("53f94265-66cb-4944-89d6-6b4c9af57090"),
    Guid.Parse("6845055e-a83b-4bb9-b76e-227486461330"),
    AssetStoreOpenMode.Create);

await using (var store = factory.Create(arguments))
{
    var metadata = await store.ReadAsync();
    // Project work; dispose every OpenResourceAsync Stream before leaving this block.
}
await app.StopAsync();
```

Пример Create предназначен для первого запуска с пустым root. Для последующего
открытия передать Mode=Open и те же сохранённые ProjectId/StoreId. Open отсутствующего,
повреждённого или чужого каталога не создаёт замену с новыми IDs. Root обязан быть
полным локальным путём; canonical UNC, включая `//server/share`, отклоняется до I/O.
Reparse/hostile-filesystem support не расширен этим App срезом.

UseNom валидирует и фиксирует options **во время подключения**, до добавления
service registrations. Позднее изменение захваченного NomOptions не меняет host.
Повторное подключение и заранее зарегистрированный NomStoreHost/typed factory
отклоняются при построении graph, не заменяют existing services.

## Публичный storage API

| Контракт | Назначение |
| --- | --- |
| IFactory<IProjectAssetStore, OpenAssetStoreArguments> | Заимствованная DI-фабрика с явными root/IDs/Create/Open/token; результат закрывает consumer |
| NomStoreHost | App-owned registry: ActiveStoreCount, IsReleased, Create, Start/Stop/Dispose; удерживает живые и failed owners |
| IProjectAssetStore | Root/ProjectId/StoreId/ContentGeneration/IsClosed, bounded metadata ReadAsync, resource reserve/publish/find/open, journal lookup/abort/prune, CompactAsync, indexed queries/exact deps, snapshots/pins/GC, sync/async disposal |
| AssetRecord | Immutable logical identity/metadata/tags/exact dependencies с defensive copies; это не bytes ресурса |
| AssetResourceWrite / AssetResourceVersion | Descriptor для borrowed input и exact immutable byte revision |
| AssetWriteTicket / AssetWriteReceipt / AssetWriteOutcome | Store-issued sequence/request и detached publication/journal outcome |

Logical IDs и публичные типы сохранены. Persistent metadata schemas и hash domains
заменены на NOM0/NOM2; старые LiteDB roots/AST tickets не совместимы. Нет implicit
migration/reset, mandatory own header/control не создаются автоматически при Open.

Resource APIs сохраняют [streaming contract](AST1B_STREAMING.md): distinct borrowed
non-seekable/short-read sources, references captured before first await,64KiB buffer,
length/hash/byte ceilings, atomic batch и journal-first retry. Returned Stream owned,
read-only/seekable; Open полностью проверяет hash тем же handle до возврата.
Publication after commit не становится cancelled. UnknownOutcomeException.Ticket
разрешается через close/reopen и LookupResourceOperationAsync; expired/aborted tickets
не исполняются заново. Файловые ошибки и duplicate binding — IOException с исходной
причиной; unsupported/corrupt logical schemas — InvalidDataException. Missing root —
DirectoryNotFoundException, Open не создаёт его. Whole publication дополнительно
ограничена4MiB KV payload; [preflight и bounded prune](CATALOG.md#дополнительные-пределы).

## Владение и lifecycle

App владеет ровно одним NomStoreHost. Typed Factory wrapper и общий NomLifecycle
не disposable и не владеют root. Один и тот же lifecycle instance зарегистрирован
как IStartable/IStoppable; DefaultApp дедуплицирует stop aliases по reference identity.
Start создаёт registry без каталога/worker. Register UseNom перед consumer startup
services; открывать project в Start/явном действии, зависимости передавать конструктором.
Ошибка последующего consumer Start откатывает уже запущенный Nom lifecycle.
Failed App.Build activation и Dispose без Start имеют resolver-owned cleanup fallback.

Consumer закрывает project stores рано; Host удаляет owner из registry только после
фактического закрытия KV handles/root unlock. App stop синхронно закрывает factory admission,
запрашивает stop **всех** owners и затем асинхронно ждёт их drain. Active readers и
writer удерживают root; источник, игнорирующий token, остаётся owned до actual return.
Отмена StopAsync отменяет только ожидание caller, cleanup продолжает работать.

При drain timeout ActiveStoreCount/root ownership сохраняются. После возврата read
leases/окончания writer можно повторить NomStoreHost.StopAsync/DisposeAsync либо
store.DisposeAsync. Repeated successful Stop/Dispose использует завершённый cleanup
attempt и не закрывает root повторно. DefaultApp.StopAsync после неудачного stop
может уже быть Stopped: для retry обращаться к сохранённому host/store, не считать
повторный App.Stop доказательством drain. Не освобождать заимствованный NomStoreHost
из consumer: это App resource.

Если одновременно failed creation и cleanup завершились ошибками, фабрика сохраняет
обе причины в AggregateException; этот branch теперь fault-injected. Transient close
fault удерживает owner thread, actual handles и registry/root до явного retry.
Повторный Stop/Dispose завершает оставшуюся close attempt на том же owner thread;
уже закрытый handle не закрывается повторно после потерянного acknowledgement. CleanupPending
удерживает единственную write reservation; replay ticket повторяет cleanup без reread.

Прежний fatal WAL/finalizer defect относится к удалённому backend и сохранён в
[исторической acceptance](FAULT_ACCEPTANCE.md#nom-faults-2026-11-07). Он не объявлен
исправленным. Новый backend требует собственной полной fault matrix NOM3.

## Backup/restore — NOM6

UseNom также предоставляет `IFactory<IProjectAssetStore, RestoreAssetStoreArguments>`;
она использует тот же App-owned NomStoreHost registry и caller-owned products. Export
через store.ExportBackupAsync, restore — в absent local root с expected identities.
No implicit migration/reset; [полный API/ownership/protocol](BACKUP.md). С DomainIndexBytes
можно явно увеличить logical budget до256MiB; default64MiB, copied options сохраняются.

## Проверки и повторение

В NOM7 повторены718/718 Nom.Tests каждый D/R,120functional groups/profile source/
published и public App34checks/profile. Fresh NuGet App34checks и NomAssetTool211checks
прошли в portable Debug/published Release и win-x64 framework-dependent Release.
[Команды/scope](HISTORY.md#nom7-2026-11-08), [delivery](DELIVERY.md).
Heavy4Capacity groups/profile и workload1k/4k/16k,50k/100k/300k,16/128MiB streams и
actual1GiBbatch — прежний [NOM6](HISTORY.md#nom6-2026-11-08), здесь не повторялись.
Products/streams/snapshot handles закрывает consumer до App; pins снимаются явно.
Physical disk-full/power-loss, другие OS/RID, self-contained/AOT/trimming и внешняя
публикация не подтверждены текущим local package proof.
