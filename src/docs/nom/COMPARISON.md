# LiteDB и SQLite по критериям Assets

> NOM2 завершён2026-11-08: [текущий backend и проверки](CATALOG.md#nom2-catalog-2026-11-08).
> Vendor runners/downloads удалены; команды и результаты этого документа исторические.

<a id="comparison-2026-11-07"></a>
## Сравнение AST0 — 2026-11-07

**Последующие решения:** сначала пользователь выбрал LiteDB, затем утвердил полную
замену собственной реализацией Nom. [Текущее решение](STORAGE_DESIGN.md#nom-own-store-2026-11-07),
[активный план](PLAN.md#nom-own-queue). Матрица ниже — история сравнения и ограничений;
ни LiteDB, ни SQLite не являются целевым backend Nom.

Основание — [инварианты самостоятельного модуля](PLAN.md), а не выбор базы для
произвольного CRUD-приложения. Сравниваются **catalog LiteDB + immutable files** и
**catalog SQLite + те же files**. Whole-database BLOB/FileStorage рассматривается
отдельно. Так влияние схемы каталога не смешивается с разными способами хранения bytes.

Это сравнение docs/versioned source и проектных свойств. Install/build/benchmark,
fault injection и delivery proof не запускались; скорости, memory footprint и
надёжности наших будущих providers оценки не присваиваются.

Проверенные точки отсчёта: [LiteDB5.0.21](https://www.nuget.org/packages/LiteDB/5.0.21)
и [.NET provider Microsoft.Data.Sqlite10.0.14](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.14).
Версия .NET provider **не** версия native SQLite. NuGet dependencies — minimum
constraints, не зафиксированный resolved graph. Перед AST1 закрепить archives,
transitive/native versions и actual `sqlite_version()`/compile options. LiteDB dev/
prerelease не смешивать с v5 documentation и source5.0.21.

## Матрица

«Преимущество» ниже — оценка стоимости реализации нашего контракта. «Обязательное»
обозначает acceptance gate: его нельзя компенсировать удобством API или скоростью.

| Критерий модуля | LiteDB | SQLite | Вывод для Assets |
| --- | --- | --- | --- |
| Независимость от Aether, App/IFactory, явный owner — обязательное | Возможен provider без engine types | То же; EF Core не требуется | Равны: границу задаёт наш API |
| AssetId/revision/tombstone и unique identity — обязательное | Guid/indexes; identity matching реализует модуль | Composite keys/unique constraints; matching также наш | Оба пригодны; IDs создаются модулем и сохраняются отдельно от digest |
| Revision/dependency integrity — обязательное | Documents/DbRef; cross-record constraints проверяет provider/module | Foreign keys/unique constraints могут проверять часть инвариантов внутри commit | Преимущество SQLite по выражению связей; FK не заменяют closure/циклы/проверку bytes. [DbRef](https://www.litedb.org/docs/dbref/), [FK](https://sqlite.org/foreignkeys.html) |
| Reverse dependencies, transitive invalidation, reachability для GC — важное | Edge collection с indexes + bounded traversal в коде | Edge table/indexes, JOIN и bounded recursive CTE | Предварительно SQLite: меньше собственной query infrastructure; это не измеренный выигрыш. [LiteDB indexes](https://www.litedb.org/docs/indexes/), [recursive SQL](https://sqlite.org/lang_with.html) |
| Metadata разных asset kinds — важное | Естественные BSON documents | Общие typed columns + versioned JSON/payload | Преимущество LiteDB по document ergonomics; SQLite поддерживает JSON, поэтому таблица на каждый kind не обязательна. [LiteDB](https://www.litedb.org/docs/), [SQLite JSON](https://sqlite.org/json1.html) |
| Metadata batch + generation pointer — обязательное | Есть BeginTrans/Commit/Rollback; transaction привязана к thread | Atomic SQL transaction, команды выполняются через owning connection | Оба кандидата; LiteDB transaction нельзя без проверки переносить через await/thread hop. [Source5.0.21](https://github.com/litedb-org/LiteDB/blob/v5.0.21/LiteDB/Client/Database/LiteDatabase.cs), [SQL transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions) |
| Snapshot readers во время import — обязательное | v5 заявляет multiple readers и write locks per collection | WAL даёт snapshot isolation/readers + один writer | Оба требуют проверки complete generation; SQLite имеет явно описанный WAL snapshot. Долгий read transaction мешает checkpoint. [LiteDB v5](https://github.com/litedb-org/LiteDB/blob/v5.0.21/README.md), [SQLite isolation](https://sqlite.org/isolation.html), [WAL](https://sqlite.org/wal.html) |
| Несколько процессов — условно важное | Direct exclusive до Dispose; Shared использует named mutex, зависит от платформы | Local processes могут читать параллельно, writers сериализуются; WAL не для network FS | Преимущество SQLite, если процессы открывают один live catalog. При одном project owner различие менее важно. [Connection modes](https://www.litedb.org/docs/connection-string/), [WAL](https://sqlite.org/wal.html) |
| Крупные bytes/streaming/atomic artifact publish — обязательное | FileStorage stream/chunks; целый upload не одна transaction | BLOB может участвовать в SQL transaction; влияние размера/streaming нужно измерить | Для catalog+external files оба требуют staged/durable objects → catalog commit и orphan recovery. FileStorage не даёт автоматического whole-asset commit. [FileStorage](https://www.litedb.org/docs/filestorage/), [SQL transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions) |
| Отмена, конечный drain и commit outcome — обязательное | Основные transaction calls синхронны; cooperative checks проектируем сами | Async provider calls синхронны; Cancel()10.0.14 — no-op | Не считать SQLite победителем по CancellationToken; обоим нужен bounded owner protocol. [LiteDB source](https://github.com/litedb-org/LiteDB/blob/v5.0.21/LiteDB/Client/Database/LiteDatabase.cs), [SqliteCommand source](https://github.com/dotnet/efcore/blob/v10.0.14/src/Microsoft.Data.Sqlite.Core/SqliteCommand.cs) |
| Case/culture invariants и canonical keys — обязательное | Default collation CurrentCulture/IgnoreCase не равен ordinal identity policy | Можно явно выбрать BINARY; LIKE имеет собственные правила | Для обоих canonical identity encoding задаёт модуль; нельзя брать default culture/query ordering для revision hash. [LiteDB collation](https://www.litedb.org/docs/collation/), [.NET SQLite collation](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/collation) |
| Schema evolution/reopen — обязательное | Schemaless не отменяет document/import schema migration | Нужны SQL migrations и payload schema versions; часть ALTER требует rebuild | Собственный versioned migrator нужен обоим; datafile upgrade не равен migration asset contracts. [LiteDB pragmas](https://www.litedb.org/docs/pragmas/), [ALTER TABLE](https://sqlite.org/lang_altertable.html) |
| Backup/export + recovery — обязательное | Проверить consistent close/export/copy policy на выбранной версии | Есть Online Backup API; live WAL нельзя игнорировать | SQLite имеет готовый catalog backup primitive; оба должны включить pinned external objects в backup generation. [Backup](https://sqlite.org/backup.html), [WAL state](https://sqlite.org/wal.html) |
| .NET/published/fresh-package поставка — обязательное | Managed C# implementation, нет собственного native SQLite runtime | Microsoft.Data.Sqlite использует SQLitePCLRaw + native SQLite bundle | Преимущество LiteDB по простоте native delivery; у SQLite отдельный RID/runtime gate. [LiteDB5](https://github.com/litedb-org/LiteDB/blob/v5.0.21/README.md), [SQLite provider/bundles](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions) |
| Лицензии поставки — обязательное | LiteDB MIT | SQLite core public domain; .NET provider MIT, весь dependency graph сверить отдельно | Оба допустимы для исследования; полный license/native inventory ещё не выполнен. [LiteDB license](https://github.com/litedb-org/LiteDB/blob/v5.0.21/LICENSE), [SQLite](https://sqlite.org/copyright.html), [provider source license](https://github.com/dotnet/efcore/blob/v10.0.14/src/Microsoft.Data.Sqlite.Core/SqliteCommand.cs) |
| Производительность, memory/disk amplification, compaction — обязательные измерения | Наших результатов нет | Наших результатов нет | Победителя нет: один dataset, один durability profile и одинаковые requested operations в AST1 |

## Сценарии, влияющие на выбор

**«Материал изменён: что переимпортировать?»** Для одного hop оба могут индексировать
edge `(target revision → dependents)`. Транзитивная closure требует visited set,
cycle handling, depth/result limits. SQL позволяет выразить обход CTE; он не делает
запрос автоматически bounded и не доказывает, что CTE быстрее application traversal.
Важны также queries «какой build удерживает revision?» и «какие objects недостижимы
от pinned manifests?». Поэтому catalog для нас ближе к графу связанных revisions,
чем к набору независимых документов. Это аргумент за SQLite, а не запрет LiteDB.

**«Новый artifact записан, приложение упало».** Если bytes лежат отдельно, transaction
любой базы не откатывает/не синхронизирует filesystem. Сначала stage/length/hash/
durability, затем metadata+dependency+ledger+generation commit. Непривязанный объект
после сбоя — recoverable orphan; published reference на отсутствующие bytes — нарушение.
Одинаковые cut points проверяются для обоих providers. Если всё хранится в SQLite,
BLOB+metadata могут входить в одну SQL transaction, но streaming/large-write/backup
стоимость ещё измеряется. LiteDB FileStorage документирует per-chunk transactions;
нельзя приравнивать Upload к одной whole-asset transaction.

**«Import отменён во время commit».** У обоих сначала проверяем token/admission,
между bounded steps допускаем отмену, затем возвращаем однозначный committed/aborted
outcome либо восстанавливаем его по operation ID. У SQLite C API есть
[sqlite3_interrupt](https://sqlite.org/c3ref/interrupt.html), но это не реализованный
Cancel() у выбранного .NET provider. Добавление interrupt-hook потребует отдельного
connection ownership/drain proof; закрывать connection, пока callback ещё использует
его native handle, нельзя. Для LiteDB per-thread transaction означает, что scheduler
должен сохранять owner thread на всём BeginTrans→Commit/Rollback участке. Ни Task.Run,
ни отмена await не доказывают, что underlying operation закончилась.

**«Runtime читает старую generation, Editor публикует новую».** Модуль должен хранить
immutable manifest и durable pin/read ownership. Не нужно держать database read
transaction весь игровой session: кратко фиксируем consistent manifest, затем читаем
exact immutable bytes по pin. Это уменьшает зависимость от database locking и WAL
checkpoint. Native database snapshot не защищает external blob от нашего GC.

**«Проект перенесён между машинами/культурами».** AssetId, locator equality, path aliases,
ordering canonical serialization и digest должны иметь общий versioned contract.
Guid/digest хранить в явном canonical representation; sorting/normalization выполнять
по правилам модуля. Culture-based index comparator не должен менять identity.
Это acceptance gate обоих providers; SQLite BINARY не обещает совпадения сортировки
UTF-8 с любым .NET comparer, а LiteDB default IgnoreCase не подходит без настройки.

## Предварительный вывод и условия смены предпочтения

Дополнительная проверка [SharedEngine5.0.21](https://github.com/litedb-org/LiteDB/blob/v5.0.21/LiteDB/Client/Shared/SharedEngine.cs):
Shared открывает engine под mutex, держит его на explicit transaction до Commit/
Rollback, а Query возвращает reader с callback закрытия. В OpenDatabase используется
`Mutex.WaitOne()` без timeout. Поэтому внутренний TIMEOUT pragma нельзя выдавать за
доказанный finite wait этого внешнего mutex. Это source-derived ограничение, не
воспроизведённый hang: в AST1 отдельно проверить lock contention/reader disposal и
stop/drain. Для первого one-owner profile Direct не требует Shared path; import
workers могут передавать complete results владельцу, Runtime — читать frozen bundle.

**SQLite — ведущий кандидат каталога**: основной вес имеют revision/dependency integrity,
reverse/closure queries и snapshot/export. **LiteDB — сильная альтернатива**, если
обязательным становится полностью managed storage, один App/project owner и компактный
набор запросов. В этом случае дополнительный application traversal может быть приемлем.
На момент сравнения backend decision ещё не было; затем пользователь выбрал LiteDB. Новых package dependencies пока нет.

Выбор нельзя сводить к «SQL всегда быстрее», «NoSQL не имеет transactions», «LiteDB
однопоточный» или «Async гарантирует отмену». Все эти формулировки здесь неприменимы.
Предварительный first profile — один write owner, bounded batches, frozen manifest
pins и общий immutable blob store; standalone App API не раскрывает BSON/SQL/types
движка. Runtime read-only bundle не обязан использовать database Editor.

## Что проверить одинаковым прототипом AST1

| Gate | Одинаковая операция/критерий |
| --- | --- |
| Identity | Content edit/reorder/rename/delete/restore, duplicate locator/case/culture; IDs/ledger сохраняются после reopen и не зависят от provider |
| Integrity/publication | Missing/corrupt bytes/dependencies, stale generation, duplicate revision, cancel до commit и race с commit; reader не видит half generation |
| Snapshots/GC | Reader держит generation G, writer публикует G+1, GC пытается удалить G; pin защищает bytes до release, затем recovery/reopen проверяет retention |
| Faults | Process kill на stage/flush/catalog commit, disk-full/locked file, corrupt/truncated catalog/blob, failed migration, interrupted backup; ожидаемый outcome и retained ownership |
| Queries | ID lookup, page by type/tag, reverse edges, transitive invalidation и reachable blobs; complete expected sets, bounded result/page и одинаковые indexes |
| Budget/performance | Одинаковые small metadata/preview и large model/audio sizes, counts/edge fan-out, durability/flush, cold/warm/reopen; p50/p95, managed allocations, process memory, database/log/staging disk size |
| Delivery/lifecycle | Source/published/fresh-package standalone app без Aether, multiple open attempts, startup failure, stop/drain/failed cleanup; SQLite actual native version/RID separately verified |

Численные corpus/budgets утвердить до запуска, raw reports сохранить; сначала пройти
behavioral gates, затем сравнивать performance. Не оценивать SQLite «быстрее» при более
слабом flush/durability режиме и не смешивать document payload size с external bytes.
