# Хранение ассетов: варианты для обсуждения

**История исследования:** первоначально пользователь выбрал LiteDB, затем утвердил
полную замену собственным Nom. [Текущее решение](STORAGE_DESIGN.md#nom-own-store-2026-11-07),
[очередь NOM0–NOM7](PLAN.md#nom-own-queue). Shortlist и предпочтения ниже сохраняют
контекст первоначального сравнения; они не задают активный provider backlog.

## Исходное требование — 2026-11-07

Самостоятельный подключаемый модуль вне движка; LiteDB рассматривается как кандидат,
есть запрос сравнить альтернативы и затем обсудить решение. [План AST0–AST3](PLAN.md)
владеет storage работами; этот документ хранит предварительные аргументы/источники.
На момент первоначального исследования backend/package/API ещё не были выбраны. Проведено чтение
официальных docs и сверка текущих engine contracts; install/build/benchmark не проводились.

Продолжение: [подробное сравнение по критериям Assets](COMPARISON.md#comparison-2026-11-07)
проверяет stable package points и versioned transaction/cancellation source. Эта
матрица уточняет shortlist ниже, не является final backend decision/performance proof.

## Короткий список

| Вариант | Почему рассматривать | Что проверить в нашем сценарии |
| --- | --- | --- |
| LiteDB catalog + content-addressed files | Managed .NET document API, удобен для различающейся metadata разных asset kinds | Dependency queries/indexes, explicit uniqueness/identity rules, concurrency/locking, migrations/recovery, commit protocol для внешних bytes |
| SQLite catalog + те же files | Relations/revisions/dependencies хорошо выражаются таблицами, constraints и indexed queries; главный альтернативный кандидат | Native package/RID delivery, migrations, foreign-key enforcement, busy/checkpoint/backup policy и тот же catalog↔blob commit protocol |
| SQLite с BLOB / LiteDB FileStorage | Проверить вариант единого database container для bytes и metadata, особенно small objects | Streaming, partial write visibility, amplification/compaction/backup, целостность при interrupted upload. Отсутствие внешних files не доказывает atomic whole-asset publish |
| Files + versioned manifests/metadata | Прозрачный baseline и возможный portable/read-only delivery | Dependency/search indexes, writer exclusion, metadata transactions/migration и recovery придётся обеспечивать самим |

Моё предварительное предпочтение — **SQLite catalog + immutable objects по SHA256**,
поскольку ожидаются exact revisions/dependency graph и запросы по связям. Это вывод
из предполагаемого workload, не измеренное превосходство. Если приоритет — полностью
managed delivery и document metadata при одном writer, LiteDB может оказаться лучшим
первым provider. Provider types/query objects не выходят в consumer API.

## Что подтверждают первичные источники

- [LiteDB overview](https://www.litedb.org/docs/) описывает embedded document storage;
  [официальный repository](https://github.com/litedb-org/LiteDB) — C# implementation.
  Это основание оценить managed deployment, но не доказательство совместимости
  конкретной версии с нашим SDK/RID или прохождения lifecycle tests.
- [LiteDB connection modes](https://www.litedb.org/docs/connection-string/): Direct
  держит exclusive file до Dispose; Shared открывает/закрывает file по operations и
  использует named mutex, multi-process возможности зависят от платформы. Нельзя
  заранее обещать одинаковую Editor/worker concurrency без выбранного owner model.
- [LiteDB FileStorage](https://www.litedb.org/docs/filestorage/): root BSON document
  ограничен16MB, FileStorage хранит большие файлы чанками, transactions выполняются
  на уровне чанка. Это не16MB лимит всего ассета; whole upload + metadata + manifest
  не становятся одной atomic transaction. Staging ID и отдельная publication policy
  нужны при использовании FileStorage так же, как для external blob protocol.
- [SQLite appropriate uses](https://sqlite.org/whentouse.html) включает local application
  data/media catalogs; [foreign-key docs](https://sqlite.org/foreignkeys.html) требуют
  явно проверить включение enforcement. Наличие SQL schema само по себе не гарантирует
  complete dependency closure/correct IDs — это контракт нашего модуля.
- [SQLite WAL](https://sqlite.org/wal.html): local readers и writer могут работать
  одновременно, writers сериализуются. WAL не предназначен для network filesystem;
  active WAL участвует в persistent state, простой copy одного open `.db` не считать
  consistent backup. Версию/native runtime и актуальные upstream fixes закрепить перед
  spike, проверить actual runtime, backup/checkpoint/reopen на pinned provider.
- [Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/)
  позволяет обойтись без EF Core. [Async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async)
  предупреждают, что async methods выполняются синхронно. Owner scheduler/queue,
  bounded admission и semantics отмены проектируются явно; Async suffix не является
  гарантией неблокирующего disk I/O.
- [SQLite internal/external BLOB study](https://sqlite.org/intern-v-extern-blob.html)
  зависит от object size/page size/OS/hardware и содержит старые измерения. Не переносить
  оттуда threshold/скорость на наши meshes/audio; сравнить small и large objects самим.

Это documentation-based assessment на2026-11-07. Stable package versions, лицензии
включённых dependencies, binary/native delivery и pinned-source semantics следует
проверить в AST0/1; repository branch и online docs не заменяют versioned proof.

## Решения, которые обсудим перед реализацией

1. Где authoritative identity/metadata: portable project records с rebuildable index
   либо durable catalog database с обязательным consistent export? ID ledger с
   tombstones нельзя терять при очистке cache; два независимых source of truth не вводить.
2. Какие данные managed module хранит: originals, generated artifacts, thumbnails,
   import settings/history; какие directories принадлежат проекту, cache и read-only
   distribution? Нужны ли Git-friendly records или единый project container?
3. Кто writer: один App/project owner с import workers, передающими captured results,
   либо независимые приложения/процессы с shared writes? Multi-machine collaboration
   и remote object storage требуют отдельного provider/protocol и не следуют из LiteDB/SQLite.
4. Нужен ли один переносимый файл в runtime или достаточно directory manifest+objects?
   Editor database format и distribution container не обязаны совпадать.
5. Реальный corpus: asset count/dependency fan-out, small metadata/preview objects,
   multi-MiB models/audio, full-project import/reimport/rename/delete и snapshots
   во время writer activity. Numeric budgets и acceptable results выбрать до замеров.

Для AST1 важнее проверить cancel/commit/crash/GC consistency, чем сравнить только
скорость одиночного insert. Filesystem CAS не делает metadata transaction атомарной
с файлом: сначала durable complete bytes, затем publication references; после crash
нужен recovery незарегистрированных objects. Power-loss durability подтверждается
отдельно от обычного process-kill test и не обещается одним atomic rename.
