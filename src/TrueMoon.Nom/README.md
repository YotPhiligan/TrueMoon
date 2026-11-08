# TrueMoon.Nom

Самостоятельное хранение ассетов вне Aether: собственный transactional KV catalog,
append-only journal и потоковая запись/чтение immutable CAS ресурсов.
**NOM2 выполнен2026-11-08:** App/resource API используют собственный core,
LiteDB/BSON implementation и зависимости удалены. Старые roots отклоняются без reset.
Название взято из лора «Сказок истинной луны»: Ном связан с порядком, знаниями и структурой.

Исходники разложены по функциональным каталогам; namespace соответствует пути:

| Каталог | Назначение |
| --- | --- |
| `Catalog/` | Метаданные, запросы, domain indexes и partial-файлы владельца каталога |
| `Resources/` | Публикация ресурсов, tickets/outcomes, streams и byte/raw buffer API |
| `Retention/` | Snapshots, pins, GC plans и retention codec |
| `Backup/` | Backup/restore, options, receipts и transfer/manifest codec |
| `Hosting/` | App-owned host, options и lifecycle adapter |
| `Storage/` | Внутренний KV, файловый IO и compaction |
| `Storage/Format/` | Binary/key/root/journal/checkpoint codecs и recovery |

В корне остаются `UseNom`, общий `IProjectAssetStore`/его facade и open arguments/mode.
Перенесённые публичные типы получили новые namespace:consumers нужно пересобрать
с импортами соответствующих подсистем. [Карта и правила](../docs/nom/STRUCTURE.md).

```csharp
using TrueMoon;
using TrueMoon.Nom;

await using var app = App.Build(context => context.UseNom());
await app.StartAsync();
```

Приложение получает `IFactory<IProjectAssetStore, OpenAssetStoreArguments>` через DI.
Фабрика открывает явный local root/Create/Open с persistent IDs. Consumer закрывает
store и read streams рано; App-owned NomStoreHost обеспечивает fallback stop/drain.
Build/Start каталог не открывают. Nom зависит только от TrueMoon.Contracts.
Проект включён в TrueMoon.slnx и формирует NuGet-пакет; самостоятельный package README —
[PACKAGE.md](PACKAGE.md). NOM7 delivery runner проверяет fresh isolated feed/cache и
четыре публичных consumers без ProjectReference в package graphs.

- [App API, пример и ownership](../docs/nom/APP_INTEGRATION.md#nom-app-2026-11-07).
- [Catalog records и recovery](../docs/nom/CATALOG.md#nom2-catalog-2026-11-08), [KV](../docs/nom/KV_CORE.md), [format](../docs/nom/FORMAT.md).
- [План](../docs/nom/PLAN.md), [статус](../docs/nom/STATUS.md), [история](../docs/nom/HISTORY.md#nom2-2026-11-08).

Из src, .NET10/PowerShell7:

```powershell
& ./ManualTests/TrueMoon.Nom.Probe/Verify.ps1 -EvidenceDirectory TestResults/MyFreshNomCore
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -EvidenceDirectory TestResults/MyFreshNomApp
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -Consumer Buffer -EvidenceDirectory TestResults/MyFreshNomBuffers
& ./ManualTests/TrueMoon.Nom.Probe/VerifyApp.ps1 -Consumer Raw -EvidenceDirectory TestResults/MyFreshNomRaw
& ./ManualTests/TrueMoon.Nom.Probe/VerifyPackages.ps1 -EvidenceDirectory TestResults/MyFreshNomPackages
```

Первый runner проверяет реальные projects без внешних packages, source Debug и
published Release, catalog/streaming/child recovery и low-level KV. Второй создаёт
независимый public App consumer без friend access. [Scope](../ManualTests/TrueMoon.Nom.Probe/README.md).
OpenResourceAsync полностью проверяет hash тем же открытым handle до возврата.
Decode, shader compilation и GPU streaming принадлежат consumers.
[Byte buffer API](../docs/nom/BUFFER_API.md#nom8-buffer-api): borrowed Memory publication,
explicit bounded Span copy, whole-resource read в caller Memory и detached read-only load.
Memory остаётся у caller до actual завершения Task; Span snapshot готов до возврата Task.
[NOM8b raw T:unmanaged](../docs/nom/BUFFER_API.md#nom8b-raw-api) добавляет typed
publication/copy/read без serialization:checked byte sizing, runtime layout/padding,
divisible exact length, element return count и caller ownership. Managed T отвергается
компилятором; portable codec остаётся ответственностью consumer.
[NOM4 CompactAsync](../docs/nom/COMPACTION.md) явно checkpoint-ит каталог и ротирует
journal, сохраняя resource outcomes/CAS/generation. Завершить reservations и закрыть
read streams до вызова. [NOM5 indexes/snapshots/pins/offline GC](../docs/nom/RETENTION.md) реализованы: tags/exact deps, frozen manifests, durable pins, bounded dry-run/HasMore slices и explicit cleanup resume. Snapshot handles и streams закрывает consumer. NOM6 [backup/restore](../docs/nom/BACKUP.md) и [workload/limits](../docs/nom/PERFORMANCE.md) реализованы: typed App restore factory, verified immutable directory, configurable domain budget (default64MiB сохранён), measured pages/metadata/streaming. NOM7 [package delivery](../docs/nom/DELIVERY.md) проверяет четыре согласованных пакета, public App consumer и самостоятельный [NomAssetTool](../ManualTests/NomAssetTool/README.md) в fresh NuGet projects. Scope — Windows x64, portable Debug/published Release и win-x64 framework-dependent Release. Isolated-volume disk-full проверен отдельно; actual VM crash verification отложена, hardware power-off исключён пользователем. Engine bridge согласуется отдельно.
