# Структурированная диагностика TrueMoon

Профиль CORE6a–c от 2026-10-25, emergency/run retention и операции CORE6d дополнены 2026-10-26.
Собственный API, .NET 10;
Microsoft.Extensions.Logging и внешние collectors не требуются.
[План](PLAN.md#core6-структурированная-диагностика--2026-10-25),
[актуальные проверки и ограничения](STATUS.md).

## Подключение к App

```csharp
using TrueMoon;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

await using var app = App.Build(context => context.UseDiagnostics(
    new DiagnosticPipelineOptions
    {
        JsonDirectory = Path.Combine(Path.GetTempPath(), "TrueMoon", "ExampleDiagnostics"),
        JsonUseRunDirectories = true,
        MinimumLevel = DiagnosticLevel.Information,
        SourceMinimumLevels = new Dictionary<string, DiagnosticLevel>
        {
            ["MyGame.NoisySubsystem"] = DiagnosticLevel.Warning
        }
    },
    legacy => legacy.Filters("MyGame")));

var events = app.Services.Resolve<IEventsSourceFactory>()!.Create("MyGame.Scene");
events.Write(DiagnosticLevel.Information, 2001, "Game.SceneLoaded", "Loaded {Scene}",
    () => new Dictionary<string, string?> { ["Scene"] = "room" });

var pipeline = app.Services.Resolve<DiagnosticPipeline>()!;
var recent = pipeline.Read();                 // oldest-first immutable snapshots
var sinkHealth = pipeline.Health();           // independent output counters
var emergency = pipeline.EmergencyStatus;     // independent internal-failure output
var runDirectory = pipeline.JsonDirectory;   // actual GUID directory after first write
var retention = pipeline.JsonRetentionStatus;
pipeline.SetSourceMinimumLevel("MyGame.Scene", DiagnosticLevel.Debug);
pipeline.SetEventEnabled("Game.SceneLoaded", false);

using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
await pipeline.FlushAsync(deadline.Token);    // only writes accepted before this call
```

Resolution в примере относится к composition root. Прикладные сервисы получают
`IEventsSource<T>` через constructor injection; factories/sinks создаются в graph
приложения, IO resources принадлежат pipeline. Повторный `UseDiagnostics` заменяет
прежнюю конфигурацию и registrations, не создавая прежние sink factories.

Legacy `UseDiagnostics(action)`, `OnEvent`, `DiagnosticEventLevel` и object payload
сохранены. `OnEvent` выполняется синхронно и сохраняет прежние payload references;
его callback должен быть быстрым. Opt-in pipeline получает отдельный bounded
snapshot до callbacks. Новый `Write(level, id, name, template, properties, error)`
проверяет level/source/event gates до вызова properties factory.
При наличии legacy observers событие может быть нужно им даже при отключённом
асинхронном сборе; это сохраняет совместимость, а не отменяет их синхронный контракт.

## События и фильтры

`DiagnosticRecord` содержит UTC time, collector sequence, source, level,
EventId/EventName, unformatted template, frozen string properties, отдельные
exception type/message/stack, Caller и Activity TraceId/SpanId на момент emission.
Произвольные payload graphs и exception objects в очередь не переносятся.
Неизвестный legacy object отображается именем типа без вызова его ToString.

`ApplicationId` идентифицирует **collecting pipeline**. DiagnosticListener остаётся
process-global: подходящие источники других Apps могут наблюдаться по прежним
discovery filters. Это не App-private routing и не remote producer identity.
Sequence задаёт порядок в collector; UTC и sequence разных процессов не являются
общими causal clocks. Для imported child records Origin хранит исходные
ApplicationId/Sequence, а ProcessId и UTC остаются emitting process values.
Операции, IPC propagation и Meter описаны ниже и в [Enerit API](../enerit/DIAGNOSTICS.md).

Live `MinimumLevel`, `SetSourceMinimumLevel` и `SetEventEnabled` действуют для
существующих источников. Для source rules побеждает самый длинный ordinal prefix,
заменяя global minimum. Максимум64source rules и256disabled event names.
`SinkMinimumLevels` задаёт дополнительные copied thresholds для `jsonl`,
`console`, `custom-0` и следующих custom sinks; memory history при этом сохраняется.

## Budgets первого профиля

| Область | Default / semantics |
| --- | --- |
| Sink queue |256records, включая выполняющийся write;16slots зарезервированы для Error/Critical;4MiB estimated retained bytes |
| Recent memory |512records /4MiB estimated bytes, eviction старейшего; caller-owned Read snapshots независимы |
| Event snapshot | message2048char; каждое exception field4096char;≤16properties, value256char, key/source/name/caller128char |
| JSONL |4numeric slots по≤1MiB UTF8 каждый, newline включён; prefix events и exclusive owner lock |
| Managed JSONL runs | opt-in JsonUseRunDirectories; закрытые runs≤8 /32MiB /7суток от создания; active/unknown отдельно |
| Emergency | enabled;8reports включая in-flight;16attempts за жизнь pipeline;≤1024chars/report вместе с LF |
| Dispose wait |5s на попытку, configurable до30s; timeout не освобождает resources исполняющегося sink |

String clipping не разрезает surrogate pair, collisions укороченных keys сохраняют
первое значение и помечают record.IsTruncated. Memory bytes — консервативный
accounting estimate, не замер managed heap. Count и field limits задают конечный
retained набор; zero allocation/performance SLA не заявлены.

Каждый sink имеет отдельный worker и очередь; producer не ждёт IO или свободного
места. При переполнении текущая запись отклоняется, включая Error при полном
error budget. Health возвращает Accepted/Written/Dropped/Failures, queued count/
bytes и bounded LastError. Ошибка одного sink не прерывает остальные; после ошибки
обрабатывается следующее событие. Custom sinks предоставляются borrowed
`IFactory<IDiagnosticSink>` в SinkFactories; результат имеет единственного owner,
pipeline. Передавать один sink двум registrations запрещено.

JSON sink сериализует records на worker. Один record, не помещающийся в file byte
budget, отклоняется с observable failure. На следующем открытии удаляются только
собственные numeric slots сверх нового maxFiles; чужие prefixes/имена сохраняются.
Нельзя использовать один directory/prefix одновременно двумя владельцами.
Flat mode ограничивает файлы данного prefix в данном directory. Для общего
count/byte/age budget закрытых запусков включите managed mode ниже.

## Аварийный канал самого pipeline

DiagnosticPipelineOptions.Emergency копирует DiagnosticEmergencyOptions.
По умолчанию отдельный worker заимствует Console.Error; можно передать Writer
или отключить Enabled. Pipeline никогда не освобождает borrowed writer.
Этот канал не вызывает DiagnosticListener, обычные sinks или application events.
Report содержит collector App ID, Stage, Sink, тип исключения и optional sequence;
payload, exception.Message/stack и объекты исключений в него не переносятся.
Control characters заменяются пробелами, каждый report имеет один literal LF.

Сообщаются capture/write/dispose/startup failures, недоставленные Error/Critical
при queue overflow, retention failure/busy/overbudget и shutdown timeout, пока
emergency admission ещё открыт. Обычные queue drops видны в sink health.
Сбой самого emergency writer увеличивает только его Failures/LastError;
он не создаёт новый report. Reentrant writer может вызвать обычное событие,
но суммарный lifetime budget предотвращает бесконечный output.

EmergencyStatus отдельно показывает Accepted/Written/Dropped/Suppressed/Failures,
Queued включая in-flight и bounded LastError. MaxReports ограничивает попытки,
включая rejected при полной очереди; последующие попытки только Suppressed.
MaxMessageChars включает LF и задаётся от128до4096; QueueCapacity от1до128,
MaxReports от1до1024. IO и форматирование происходят на отдельном worker;
producer не ждёт writer. Это best-effort fallback для ошибок диагностики,
не копия потерянного event и не аварийный dump при crash процесса.

## Хранение JSONL между запусками

JsonUseRunDirectories=true трактует JsonDirectory как постоянный managed root.
Каждый sink лениво создаёт GUID-каталог при первом write; versioned
.diagnostic-run marker содержит UTC создания и удерживается exclusive до
закрытия файлов. JsonDirectory у pipeline и DirectoryPath у sink возвращают
фактический каталог; до первого managed write они null.

JsonRetention копирует DiagnosticRunRetentionOptions: MaxRuns=8,
MaxTotalBytes=32MiB, MaxAge=7суток. Учитываются все файлы распознанных закрытых
runs, включая marker/lock; age отсчитывается от UTC создания, равенство лимиту
допускается. Удаляются старейшие по creation UTC, затем ordinal имени при
равенстве. Самый недавно созданный закрытый run и явно защищённый текущий
сохраняются даже сверх лимита; IsOverBudget отражает невыполненный budget.

Sweep выполняется на sink worker перед первым write и после закрытия файлов,
без фонового age-таймера. Общая .retention.lock согласует cleaners разных Apps;
занятая lease даёт MaintenanceBusy без ожидания. JsonRetentionStatus сообщает
RetainedRuns/RetainedBytes, ActiveRuns/SkippedDirectories, DeletedRuns,
Failures/LastError и IsOverBudget за последний pass. Maintenance failure не
превращает успешный record write в ошибку и сообщается аварийному каналу.

Active runs исключены из closed-run budget, каждый ограничен JsonMaxFiles и
JsonFileSizeBytes. Поэтому суммарный диск включает closed budget плюс каждый
активный run. Неизвестные/legacy каталоги, flat files и чужие prefixes не
присваиваются и не удаляются. Удаление только нерекурсивное: valid GUID/marker
и известные events.lock/events-N.jsonl; reparse ancestry/entries отклоняются.
Managed constructor принимает только prefix events. Старый четырёхаргументный
JsonDiagnosticSink и default flat mode сохраняются.

После abrupt death освобождённый marker позволяет убрать распознанный run,
в том числе с частичным JSONL, при следующем sweep. Повреждённый marker или
неизвестные файлы сохраняются для inspection. Ownership предполагает trusted-local
cooperating writers; hostile path replacement между проверкой и удалением,
power-loss durability и общий quota всех приложений/артефактов не гарантируются.

## Трассы и метрики операций

Базовый App регистрирует один lazy App-owned `IDiagnosticTelemetry`, доступный
без отдельного включения sinks/subscription. `UseDiagnostics` заменяет его,
включая legacy overload без pipeline. Внедряйте его в сервис через конструктор.
Опции `DiagnosticPipelineOptions.Telemetry` копируются при setup;
`TracingEnabled` и `MetricsEnabled` можно независимо отключить.

```csharp
using var operation = telemetry.StartOperation("Game.Load", () =>
    new Dictionary<string, string?> { ["SceneId"] = sceneId });
try
{
    await LoadAsync(token);
    operation?.Complete();
}
catch (OperationCanceledException error)
{
    operation?.Complete(DiagnosticOperationOutcome.Cancelled, error);
    throw;
}
catch (Exception error)
{
    operation?.Complete(DiagnosticOperationOutcome.Failed, error);
    throw;
}
```

`ActivitySource` и `Meter` имеют имя **TrueMoon**. Подключение стандартных
`ActivityListener`/`MeterListener` или exporter остаётся решением приложения.
Без listeners scope не создаётся. Sampling None не создаёт Activity;
PropagationData сохраняет связь trace/span без пользовательских свойств.
Properties вызываются только при `IsAllDataRequested`; metrics-only collection
не вызывает их. Structured events внутри операции получают текущие TraceId/SpanId.
Async-операция продолжает `Activity.Current`, Dispose выполняется в том же async flow
в обратном порядке вложенности. Overload StartOperation(name, ActivityKind, parentContext,
properties) задаёт client/server kind и explicit parent; null использует текущий flow.
Третий аргумент обязателен, чтобы прежний StartOperation(name, default) сохранял
однозначный выбор API. Default interface implementation перенаправляет вызов в прежний
overload; custom owners обновляются для точных kind/remote-parent semantics.
[Enerit pipes propagation](../enerit/DIAGNOSTICS.md) использует этот overload.

Два инструмента: `truemoon.operation.count` (Counter Int64) и
`truemoon.operation.duration` (Histogram Double, секунды, monotonic clock).
Единственные metric tags — `operation` и `outcome`. Исходы: Succeeded, Failed,
Cancelled, Incomplete. Session/build/generation/AppId/PID не являются metric tags.
Первая Complete фиксирует outcome; повторные Complete/Dispose безопасны.
Scope без Complete имеет Incomplete. Error сохраняет только имя типа; Failed trace
имеет Error status. Trace содержит `truemoon.app.id`, совпадающий с ApplicationId
pipeline при opt-in подключении; legacy diagnostics остаётся process-global.

По умолчанию принимаются64различных имени операции (предел256), длина имени/ключа128
(до512),16проверяемых свойств (до64), значение256символов (до4096).
Новые имена сверх лимита и слишком длинные имена/ключи отклоняются;
значения обрезаются без разрыва surrogate pair. Имена, дошедшие до sampling,
занимают бюджет до освобождения owner, включая sampling None.
Reserved `truemoon.app.id`, `outcome`, `error.type` не перезаписываются properties.

`DiagnosticTelemetry.Read()` возвращает ActiveOperations/RejectedOperations/Failures
и Closed/Released. Callback/property failures изолированы; рекурсивное создание
операции из callback отклоняется. Callbacks синхронны: произвольный блокирующий
listener нельзя прервать. Borrowed ActivitySource/Meter доступны на конкретном
owner для выбора observer по reference; освобождать их должен только owner.
Ошибка listener при создании source оставляет ActivitySource=null; metrics могут
продолжать работу. Ошибки публикации instruments учитываются в Failures.
Dispose закрывает новые операции, а source/meter сохраняются до Dispose последнего
активного scope. Все scopes потребителя должны завершаться до его App shutdown.

## Чтение и импорт bounded history

Opt-in UseDiagnostics(options) регистрирует IDiagnosticRecordReader,
IDiagnosticRecordImporter, IDiagnosticHealthReader, IDiagnosticArchiveReader и
IDiagnosticConfigurationReader как borrowed nondisposable views одного App-owned
pipeline. Legacy overload без pipeline их не предоставляет. Повторная конфигурация
заменяет registrations; разрешение aliases не создаёт дополнительных owners.

LatestSequence включает unavailable capture slots. ReadAfter(afterSequence,
maximumCount) возвращает independent oldest-first snapshot оставшихся records
после exclusive cursor; cursor неотрицательный, maximumCount от1до1024. Eviction
может оставить gaps. Это pull API без callbacks на emitter thread.

`IDiagnosticHealthReader.ReadHealth()` возвращает cached in-memory collector/ring,
sink queue/write/drop/failure, emergency и managed-run retention snapshots. Чтение
не делает IO/flush/maintenance и не ждёт workers. Состояния отдельных workers сняты
независимо; null optional status означает disabled/not-yet-available, не healthy0.
Ring eviction, capture failures, reader sequence gaps и sink loss — разные counters.
Accepted/Written не подтверждают disk durability. LastError — bounded display text,
может содержать личные пути; report consumer должен явно решить его privacy policy.
После close final snapshot остаётся доступным, reader не освобождает pipeline.

TryImport(record, context) назначает host ApplicationId/Sequence и Origin с
исходными identity/sequence. Timestamp, emitting PID, trace/span и exception strings
сохраняются с локальными clipping/property budgets; malformed trace/span очищаются
вместе. Context properties копируются первыми и имеют приоритет при collisions.
Импорт применяет live source/event filters и обычные memory/sink limits, не вызывает
DiagnosticListener. Accepted означает collector admission; sinks и ring всё ещё
могут терять records согласно своим budgets. Records с Origin уже заданным,
некорректной identity/sequence/PID/level отклоняются; closed pipeline считает LateWrites.

Reader/importer сами не создают transport. Aether подключает их через отдельную
[child → host delivery lane](../engine/DIAGNOSTICS_DELIVERY.md), сохраняя child
JSONL и raw stdout/stderr. Это не меняет process-global routing обычных listeners.

## Архив JSONL и действующие настройки

`IDiagnosticArchiveReader.ReadHistoryAsync(options, token)` читает только настроенный
logger root: flat slots либо recognized managed runs с корректным marker. Старые
recognized runs могут иметь другое число rotation slots; их файлы ограничиваются
read budgets, а не текущим JsonMaxFiles. Текущий
run читается без ожидания его marker lease; чужой active/недоступный run может быть
пропущен. Неизвестные каталоги, посторонние файлы и reparse points не читаются.
Чтение не выполняет flush, retention, запись или ожидание других sinks. Caller
владеет принятой задачей и отменяет/drain её до освобождения своих зависимостей.

Default budgets:512records,4MiB фактически прочитанных bytes,2MiB estimated retained
payload,16files,64KiB/line и cooperative2s. Discovery ограничен256entries. Читаются
хвосты файлов, начиная с недавно изменённых; incomplete boundary/final lines,
malformed/oversized lines и disappearing/unavailable files учитываются отдельно.
Records заново ограничиваются стандартными Core field budgets и замораживаются;
сохраняются collector identity и child Origin. Дедупликация действует в retained
window по ApplicationId/Sequence; результат отсортирован по timestamp/identity,
не обещает глобального causal order. `IsTruncated`, omissions и time budget
отделены от capture/sink loss. `Available=false` означает отсутствие JSONL capability.
Independent snapshots не гарантируют полноту async хвоста или disk durability;
cooperative deadlines не прерывают произвольный зависший filesystem call.

`IDiagnosticConfigurationReader.ReadConfiguration()` без IO возвращает frozen
scalar settings: текущий minimum level, размеры queues/ring/fields, retention,
telemetry/emergency flags и количества filter rules. Пути, имена правил, delegates,
environment и endpoints не входят в контракт. Source/event overrides продолжают
действовать; их количества не заменяют подробную конфигурацию фильтров.
Оба access aliases не IDisposable; владельцем pipeline остаётся App.
Повторный legacy UseDiagnostics снимает все пять aliases.
[Editor consumer и политика передачи текста](../engine/EDITOR_PLAN.md#ed8a-экспорт-диагностического-отчёта--2026-10-25).

## Завершение и ошибки

App Dispose закрывает admission, дожидается принятых sink writes и освобождает
sinks; Stop оставляет сбор доступным для subsequent cleanup events. RunAsync
выполняет Dispose своим штатным lifecycle. Flush cancellation отменяет только
ожидание. После disposal timeout pipeline сохраняет worker/sink ownership; можно
повторить DisposeAsync. Arbitrary зависший custom IO не прерывается принудительно.
При startup factory failure уже созданные sinks закрываются; ошибки rollback
сохраняются вместе с исходной причиной.
Один ShutdownTimeout покрывает normal cleanup и emergency drain; normal sinks
закрываются перед emergency admission. После timeout исполняющееся IO остаётся
owned до завершения/retry. Если завис сам emergency writer, наблюдаемы timeout
и Queued; report в уже закрытый канал не добавляется. Constructor rollback
использует общий budget и продолжает закрытый cleanup после timeout.
Flush сначала ждёт normal-sink fences, затем emergency reports, принятые к
моменту второго fence. Отмена ожидания не отменяет запись и не освобождает writer.

Read/Health, MemoryEvicted, CaptureFailures и LateWrites доступны для диагностики
самого pipeline без рекурсивных логов. Abrupt kill/power loss может потерять async
хвост; Flush не обещает fsync/durable audit delivery.

## Перенаправленные stdout/stderr

`ProcessOutputCapture` из `TrueMoon.Core` — отдельный low-level utility без App/DI
зависимости. Он заимствует TextReader, сразу запускает drainage, хранит tail
(default32Ki UTF16 chars) и при необходимости пишет bounded UTF8 file
(default1MiB). Файловый worker получает максимум8chunks; очередь переполнена или
файл недоступен — чтение pipe продолжается. Newline не требуется.

`Read()` доступен до EOF: Text, TotalCharacters/DroppedCharacters,
FileBytesWritten/FileBytesDropped и FileError. FileBytesWritten учитывает успешно
завершившиеся записи; частичный IO failure может оставить неполную запись в файле.
Tail и file retention независимы; file loss явно обозначен counters.
`Read(maximumCharacters)` копирует только выбранное UTF16-safe tail window;
`ProcessOutputSnapshot.Limit(...)` ограничивает уже снятый snapshot. Original
TotalCharacters/DroppedCharacters и file counters сохраняются. Новое
OmittedCharacters считает только исключённые меньшим display window символы,
отдельно от capture eviction/file loss. Нулевое window даёт counters без текста.
Прежние `Read()` и шестипараметрический constructor snapshot сохраняются.
Владелец процесса ждёт Completion после exit до освобождения reader/process.
Read failure передаётся через Completion; timeout ожидания не означает drain.

Aether использует utility в NativeRuntimeLease и GameBuildCompiler. Подробнее —
[engine diagnostics](../../TrueMoon.Aether/README.md#диагностика).
С 2026-10-26 Aether отдельно ограничивает completed build jobs по count/total
bytes/age через BuildRetention; active/legacy/incomplete jobs и game artifacts
сохраняются. Этот engine budget независим от managed Core JSONL run retention.
