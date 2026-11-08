# Nom: размещение исходников и namespace

<a id="nom-source-layout-2026-11-08"></a>
## Функциональные подсистемы — 2026-11-08

По запросу пользователя исходники библиотеки разложены по назначению, вместе с
относящимися к подсистеме публичными типами, интерфейсами и реализациями. Namespace
совпадает с реальным путём относительно TrueMoon.Nom.csproj. Категорий по виду типа
(Contracts/Models/Interfaces) нет. Тесты и probes остаются в своих проектах.

| Путь / namespace | Назначение и примеры |
| --- | --- |
| Корень / `TrueMoon.Nom` | Общий facade:AppConfigurationExtensions.UseNom, IProjectAssetStore, ProjectAssetStore, OpenAssetStoreArguments, AssetStoreOpenMode |
| `Catalog/` / `TrueMoon.Nom.Catalog` | AssetRecord/AssetQuery, CatalogOwner/CatalogFactory и все owner partial-файлы, NomCatalog/его partials, metadata codecs/domain index |
| `Resources/` / `TrueMoon.Nom.Resources` | AssetResourceVersion/Write, tickets/outcomes/exceptions, stream adapters, byte и unmanaged raw extensions |
| `Retention/` / `TrueMoon.Nom.Retention` | IAssetSnapshot/AssetSnapshotManifest, snapshot handle, GC plan/receipt и retention codec |
| `Backup/` / `TrueMoon.Nom.Backup` | Restore arguments, backup options/receipt/exception, manifest codec и transfer |
| `Hosting/` / `TrueMoon.Nom.Hosting` | NomOptions/NomSettings, NomStoreHost и отдельно NomLifecycle |
| `Storage/` / `TrueMoon.Nom.Storage` | NomKvStore со всеми partials, factory/types, filesystem IO и compaction receipt/exception |
| `Storage/Format/` / `TrueMoon.Nom.Storage.Format` | NomBinary, key/root/journal/checkpoint codecs, limits и recovery records |

CatalogOwner остаётся одним partial-типом:его backup/resource/snapshot/GC orchestration
файлы лежат в Catalog. Так же все partials NomCatalog и NomKvStore лежат рядом с
основным типом. Перенос в каталог другого namespace не разделяет существующий класс.
NomLifecycle выделен из AppConfigurationExtensions в отдельный Hosting файл;
регистрации, создание и stop/drain/dispose сохраняют прежний порядок.

## Совместимость consumers

Перенос публичных типов меняет их fully qualified names. Для текущего alpha API
принято обновить исходники consumers и пересобрать их; binary compatibility со
старыми compiled clients не обещается. Compatibility aliases/дубли типов не добавлены.
IProjectAssetStore возвращает типы подсистем, поэтому даже клиент общего facade
должен пересобраться. `UseNom` остаётся root extension, его options — Hosting.NomOptions.

```csharp
using TrueMoon.Nom;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Hosting;
using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage;
```

Достаточно импортов используемых подсистем. Encoding/формат, bytes/digests/revisions,
resource IDs и App ownership не используют CLR namespace как данные и не меняются
этой реорганизацией. Functional checks и fresh-package delivery подтверждаются
отдельным новым запуском в [STATUS](STATUS.md), [HISTORY](HISTORY.md#nom-structure-2026-11-08).

## Проверяемые условия

- Каждый исходный файл присутствует в карте переноса; namespace соответствует пути.
- Все declarations одного partial-типа имеют один namespace.
- Содержимое implementations/tests сопоставляется с Before snapshot без using,
  namespace и whitespace; NomLifecycle учитывается как точное выделение из прежнего файла.
- Default SDK glob включает все вложенные66C# файлов; project/package references
  не требуют ручного Compile Include и не меняются.
- Source freeze регрессионного runner рекурсивный и исключает bin/obj; package PE
  audit проверяет ожидаемые public namespace, internal API остаётся недоступным.
- Четыре external consumers и managed-T compiler rejection используют новые imports.

Исторические результаты сохраняют прежние даты и namespace в описаниях; известные
локальные source-link targets обновлены по карте переноса. Текст архивов сохраняется
с этой явной адаптацией ссылок; старые source snapshots остаются в evidence. Новая
сборка не подтверждает заново прежние VM/physical/GPU или heavy capacity результаты.
