# Enerit: план IPC и генерации invocation

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Планируемые semantics и новые API уточняются перед соответствующим этапом.

## Границы

TrueMoon.Enerit, Enerit.Generator, Enerit.Tests и Enerit.Generator.Tests. Invocation client/server, handler resolution, сериализация, pipes/memory-mapped transports и generated service adapters.

Запуск и перезапуск процесса относится к [Mithril](../mithril/PLAN.md), app lifecycle и общие service contracts — к [Core](../core/PLAN.md), DI registration/resolution — к [Cobalt](../cobalt/PLAN.md). Транспорт не обязан управлять lifetime процесса.

## Цель

Два собственных процесса выполняют типизированный invocation через один заявленный transport, передают поддержанные значения, cancellation и ошибки, а завершение client/server не оставляет каналов или ожиданий. Возможности второго transport подтверждаются отдельно.

## Архитектура и решения

Уточнение2026-10-22: [Aether DI1–DI4](../engine/PLAN.md#приведение-к-правилу-cobaltifactory)
добавляет типизированные PipesInvocationClientFactory<T>/PipesInvocationServerFactory<T>
с IFactory<TProduct,TArguments>. App invocation registrations получают products
через общий IFactory поверх прежнего transport-specific factory. Wire и generated
handlers прежние; caller владеет динамическими products, зарегистрированные App
transports сохраняют lifecycle.

Сохранять IInvocationClient/IInvocationServer, factories/handlers и ISerializer boundaries. Runtime использует Contracts/Core и свой generator. Transport choice остаётся явным, generated adapters должны соответствовать runtime framing/serialization.

До утверждения production readiness определить framing/size limits, method mapping, совместимость payload, cancellation/timeout/disconnect semantics и владение буферами/каналами. Чтение существующих interfaces не означает, что эти гарантии уже выполнены. Pipes и memory-mapped реализации не объявляются равно проверенными.

### Совместимость с Core ownership — 2026-10-10

Исправление pools/scheduler Core потребовало ограниченных изменений pipes: deserialized bytes используют tracked whole rental; client/server владеют schedulers и connection tasks. Dispose прерывает connect/read/write, отклоняет новые invocation, завершает pending responses и дожидается async handlers до закрытия scheduler, нужного их continuations. Request/response buffers возвращаются в finally. Cancellation/disconnect/invalid или truncated response не оставляют ожидание до штатного50s timeout. Headers читаются полностью, EOF наблюдаем.

Эти контракты подтверждены Windows in-process regression tests ([HISTORY](HISTORY.md#совместимость-pools-и-завершение-pipes--2026-10-10)). Framing не изменён; own-process transport, общий payload-size policy, MMF и межверсионная protocol compatibility остаются задачами ENE1–ENE3.

### ENE1 ENE2 контракт — 2026-10-11

Принят ограниченный Windows x64/net10.0 pipes baseline: оба peers и generated
adapters собраны из одинакового service/payload source одной версией генератора.
Это уточнение payload/error semantics, а не подтверждение protocol versioning,
межплатформенного ABI или всего G3 composition/restart сценария.

- Request/response frame: Guid (16 bytes), method/status (1 byte), signed payload
  length (4 bytes), затем payload. Допустимы 0–1 048 576 bytes без header.
  Входная длина проверяется до rental/dispatch. Полный frame пишется под одним
  write gate; concurrent responses коррелируются по Guid. Очереди пока без backpressure.
- Generated payloads: bool/byte/int/long/float/double, int-backed enums,
  Guid/DateTime/DateTimeOffset/TimeSpan, nullable поддержанные value types,
  string/string?, Memory<byte>/ReadOnlyMemory<byte>, одномерные arrays,
  List/IReadOnlyList и Dictionary/IReadOnlyDictionary с поддержанными элементами.
  Mutable DTO — nongeneric, nonabstract class без inheritance/records, с единственным
  public parameterless constructor, public readable/writable properties без init-only
  и mutable instance fields. DTO graph acyclic, не глубже32 узлов.
  Null и empty strings/collections/DTO различаются presence marker; bytes имеют
  length без null marker. Nullable string использует один marker своего codec.
- Collections ограничены65 536 elements; отрицательные/слишком большие counts,
  невозможные remaining bytes, state вне0/1, negative/odd UTF-16 lengths и truncation
  дают InvalidDataException. Generated handler отвергает trailing request bytes
  и неизвестный method code; service при этом не вызывается.
- TME001 diagnostic отвергает object, decimal/ValueTask, tuples/custom structs,
  nested collection elements, неподдержанные DTO shapes и static/generic/ref/out
  service methods, properties/events и более256 методов. Declared/inherited methods
  и equivalent signature dedup сохраняются. Method IDs зависят от Roslyn order,
  стабильная схема между разными сборками пока не установлена.
- Каждый вызов, включая void/Task, получает acknowledgement. Status0 — успешный
  payload, status1 — UTF-8 remote exception text, превращаемый в InvocationException.
  Handler throw/reported failure/oversized result также получают error response;
  слишком длинный error text заменяется bounded сообщением. Неизвестный status или
  invalid frame disconnect/fault pending; EOF сохраняется как IOException subtype.
- Request cancellation локальная: pending TCS удаляется/отменяется; отправленный
  handler продолжает работу до завершения или connection shutdown. Late response
  полностью читается и возвращается в pool без reconnect. Timeout50s имеет ту же
  локальную semantics. Retry invocation автоматически не выполняется; reader
  reconnect создаёт новое соединение для последующих вызовов, с возможными
  промежуточными попытками во время server teardown.
- Transport владеет целыми request/response rentals, schedulers и channel.
  ReadBytes создаёт отдельный tracked rental: handler/caller возвращает bytes,
  в том числе вложенные DTO/collections, через TryReturn после использования.
  Автоматический обход/возврат вложенных rentals при partial DTO decode ещё не
  реализован. Synchronous Dispose ждёт handlers, поэтому игнорирование cancellation
  может удерживать shutdown; bounded proof использует cooperative handlers.

**Изменение совместимости:** void/Task теперь ждут remote acknowledgement, nullable
strings больше не получают лишний marker. Старые и новые peers смешивать нельзя.
Scalar encoding использует native layouts; canonical cross-version/architecture
wire format, response trailing-byte validation и schema negotiation остаются открыты.
Старые object/tuple/custom-struct эксперименты MithrilTest исключены из generated
service interface; сохранены обычные методы implementation, Sender их не использует.

Проверяемый runner и границы — [EneritPipesProbe](../../ManualTests/EneritPipesProbe/README.md).
ENE1/ENE2 выполнены как этот bounded baseline; общая production/package readiness
и ENE3–ENE5 из него не следуют.

## Этапы

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| ENE0 | Runtime/generator/transport baseline | Discovery/results существующих projects, активные и отключённые transport scenarios перечислены отдельно |
| ENE1 | Serialization и generated mapping contract | Supported values/null/collections и malformed/unsupported payloads имеют проверяемый результат; generated code компилируется и согласован с runtime |
| ENE2 | Один работоспособный IPC transport | Own-process invocation, concurrent requests в выбранном режиме, cancellation/disconnect/error/cleanup подтверждены с фиксированным transport |
| ENE3 | Второй transport и сопоставление capabilities | Общий сценарий повторён, transport-specific limits/failures и ownership задокументированы; неподтверждённый режим остаётся experimental |
| ENE4 | Совместный scenario с Mithril | Unit выполняет invocation, отказывает/завершается, согласованный client/server cleanup и дальнейшее поведение воспроизводятся |
| ENE5 | Package consumer и baseline | Generator/runtime/transport доставлены независимому consumer; измерены latency/throughput/allocations выбранного сценария до оптимизаций |

Начать ENE2 с transport, подтвердившего ENE0; не назначать его успешным только из-за наличия реализации. ENE4 зависит от MIT1–MIT3 и согласованной boundary между channel lifecycle и restart process.

## Критерии готовности

- Typed invocation и serialization соответствуют заявленному API; generation/diagnostics и unsupported values проверены.
- Cancellation, remote error, disconnect, invalid/oversized payload и shutdown имеют согласованный результат без зависших ожиданий.
- Client/server/channel/buffer ownership и пределы ресурсов описаны и проверены.
- Заявленные transports проверены независимо; disabled/commented tests не выдаются за successful run.
- Separate-process и package consumers воспроизводимы, generator delivery проверен вне ProjectReference.

## После первой рабочей проверки

Развивать payload compatibility, diagnostics и throughput по реальному consumer. Reconnection/backpressure и новые transports требуют отдельного contract proposal; сетевой RPC этим планом не обещается.

### ENE4 App registration и lifecycle — 2026-10-14

Детальный совместный acceptance ведётся один раз в
[MIT4/ENE4 контракте Mithril](../mithril/PLAN.md#mit4ene4-app-и-generated-pipes-контракт--2026-10-14).
Transport остаётся независимым от process controller; same-build pipes scope
ENE1/ENE2 сохраняется. MMF ENE3 из этой интеграции не следует.

Для App composition generator публикует typed registration factories в
InvocationServiceStorage. Это compiled metadata, а не global service instances.
Pipes internal services также создаются explicit factories, без зависимости на
Cobalt. Новые generated factories требуют runtime с соответствующими overloads;
старые metadata Register/RegisterHandler без factory сохранены, но type-based
fallback сохраняет ограничения используемого resolver.

PipesServiceTransport(Func<Type,string>) выбирает service endpoint без `tm_`;
peers должны согласовать его сами, default overload не изменён. Выбранные null/
empty/blank names не подменяются default. UseDiagnostics обеспечивает требуемую
IEventsSourceFactory. Local consumer implementation должна иметь явную видимую
Cobalt registration; Listen сохраняет существующий service и всё равно запускает
listener. Use сохраняет existing local registration и не создаёт remote client.
Повторный вызов idempotent. Owned transports участвуют в App Start/Stop rollback;
собственные предварительно зарегистрированные transports сохраняют ownership.

Следующий локальный шаг до ENE5: строгая validation response payload до возврата
результата и cleanup nested ReadBytes rentals при частично неуспешном decode.
Backpressure, uncooperative shutdown и schema/version negotiation требуют
отдельного решения; это не обещание production readiness после ENE4.

### ENE1/ENE2 strict response и decode ownership — 2026-10-14

Уточнение перед independent ENE5/MIT5 consumer. Same-build pipes framing и
supported payload shapes сохраняются; корректный wire format не меняется.

- Generated typed response должен потребить payload полностью: лишний byte,
  truncation, invalid state/length дают InvalidDataException до возврата результата.
  Null marker — полный null payload, successful void/Task acknowledgement пуст.
  Read<bool> принимает только0/1. Duplicate/null dictionary keys отвергаются как
  InvalidDataException, без частичного результата. Scalar enum/domain ranges этим
  structural validation не ограничиваются.
- Status1 text — строгий UTF-8. Malformed text даёт InvalidDataException, fault
  pending и reconnect; matched request остаётся в pending map до успешного decode
  error text. Valid empty/Unicode exception message сохраняется. Decode failure
  внутри корректного success frame fault только этого invocation; frame уже прочитан,
  следующее сообщение может быть обработано без reconnect.
- Явный PayloadDecodeScope принадлежит одному synchronous decode. Все ReadBytes,
  включая вложенные DTO/arrays/lists/dictionaries/keys/parameters, регистрируются
  у общего root scope. При failure scope возвращает каждую whole rental; обход
  partially assigned object graph и ambient/static ownership не используются.
- Response scope Commit выполняется после полного decode и length validation.
  Caller становится владельцем всех whole nested rentals и возвращает их после
  использования. Handler Commit выполняется после decode ВСЕХ parameters и
  validation, перед service entry. После этого service отвечает за эти rentals,
  включая свой throw/cancellation; automatic return после service вызова не добавляется.
- Public DTO Deserialize сохраняет prefix/ref offset API и переносит rentals
  caller после успешного prefix decode; остаток span может содержать следующее значение.
  Internal nested reader заимствует root scope и самостоятельно не Commit.
  Throwing user setter/constructor сохраняет исходную ошибку, failure rentals возвращены.
  Partial failure не обещает откат offset или внешних side effects DTO user code.
- Opaque manual IInvocationClient decode callback должен сам обеспечить shape/
  consumed-length validation и nested ownership; transport знает только границы frame.
  Unscoped ручной SerializationUtils.ReadBytes сохраняет существующий contract.

Проверки: direct scope ownership/guards, реальные compiled generated callbacks и
handlers с malformed и valid prefixes/collections/DTO, raw pipes invalid ack/error,
последующее сообщение и outer response rental cleanup. Затем повторить App36 и
isolated pipes12 Debug/published Release после generator changes. MMF/old generated
code/schema negotiation/backpressure/uncooperative shutdown/other OS и performance
не объявляются готовыми. Новая generation требует runtime с PayloadDecodeScope.

### ENE5 package portion — 2026-10-14

Совместный detailed consumer contract ведётся один раз у
[Mithril MIT5/ENE5](../mithril/PLAN.md#mit5ene5-independent-package-contract--2026-10-14).
Explicit Enerit analyzer/runtime доставляются в independent same-build pipes App
consumer на Cobalt/DI; Microsoft DI также без Cobalt analyzer. Generator-only layout,
compiler path/SHA, metadata/source/version и deployed own child проверяются отдельно
от source ProjectReference suite. Correct codecs/decode ownership contract сохраняется.

Package portion не означает полного ENE5: следующий отдельный шаг — измерить
latency/throughput/allocations выбранного scenario до оптимизаций. Operational
process diagnostics принадлежат MIT5; MMF ENE3, backpressure/schema/uncooperative
shutdown и другой compiler/platform требуют собственных решений/checks.

### ENE5 измерительный контракт — 2026-10-15

После завершённого bounded Core quality выбран reproducible same-build pipes
baseline. [Постоянный workload/runner](../../Benchmarks/TrueMoon.Enerit.Benchmarks/README.md)
использует реальный generated client/handler/DTO, без App/DI/Mithril и без
изменения runtime. Ось сравнения — размер/shape payload и concurrency; этот run
фиксирует исходную точку для будущих before/after измерений, не speedup.

10cases: scalar, byte echoes256/16384/65536 и DTO с1024byte buffer, каждый с1/8
outstanding calls по одному connection.10verify pairs проверяют результат и
cleanup. Затем3fresh client/server pairs на case, rotating order, warmup≥1.5s,
pilot512calls и3calibrated batches примерно1s (minimum250ms). Все90samples
сохраняют raw per-call Stopwatch ticks, elapsed/opcount, allocations и GC counts.

RTT заканчивается после typed await до validation/return rental. Batch throughput
включает validation/return/worker/clock overhead. Client/server allocations —
GC.GetTotalAllocatedBytes(true) по всем потокам каждого процесса, включая service
и небольшие harness/control/batch-boundary расходы; это не retained/native memory
и не MemoryDiagnoser одного потока. Управляющий stdin/stdout вне client окна;
server begin acknowledgement/end read overhead не вычитается. Report JSON/startup/
connect/raw arrays вне измерения, принудительного GC и изменения power plan нет.

Case summary — median9sample metrics, с throughput min/max; это3независимых pairs,
а не9независимых процессов. Closed-loop tails не являются open-loop SLA. Machine
frequency/background/OS caches не контролируются. Source/generated/published binary/
raw-report hashes и runtime/GC/CPU/SDK фиксируются; окончательные hashes/power plan
сверяются перед Completed. Матрица не обещает MMF/other OS/RID/AOT/package timings.

Критерий завершения этой части: полный10/30pair/90sample run без invocation errors,
current runtime/generator regression отдельно Debug/Release, датированный отчёт
с ограничениями и выбором следующего шага. ENE5 package portion сохраняет собственную
дату/доказательства; один baseline не завершает все будущие quality/API roadmap задачи.

Проверено2026-10-15:10verification/30measurement pairs,90samples/571944RPC,
runtime126/generator114 в каждой Debug/Release. [Числа и ограничения](BASELINE.md),
[команды/артефакты](HISTORY.md#ene5-pipes-performance-baseline--2026-10-15).
Измерительная часть ENE5 завершена в этом scope; optimization не выполнялась.
Client all-thread allocation2.2–3.0KB/call и scalar/concurrency overhead требуют
отдельного allocation/CPU profile перед оптимизацией. Это measured backlog,
не подтверждённый correctness blocker и не автоматическое требование MMF/новых API.
Следующий общий функциональный этап — Mithril MIT5 operational diagnostics.
Generated nullable-context warnings CS8669 сохраняются отдельным quality backlog;
новый benchmark проявляет10таких warnings, массового подавления нет.
