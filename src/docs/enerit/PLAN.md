# Enerit: план IPC и генерации invocation

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Планируемые semantics и новые API уточняются перед соответствующим этапом.

## Границы

TrueMoon.Enerit, Enerit.Generator, Enerit.Tests и Enerit.Generator.Tests. Invocation client/server, handler resolution, сериализация, pipes/memory-mapped transports и generated service adapters.

Запуск и перезапуск процесса относится к [Mithril](../mithril/PLAN.md), app lifecycle и общие service contracts — к [Core](../core/PLAN.md), DI registration/resolution — к [Cobalt](../cobalt/PLAN.md). Транспорт не обязан управлять lifetime процесса.

## Цель

Два собственных процесса выполняют типизированный invocation через один заявленный transport, передают поддержанные значения, cancellation и ошибки, а завершение client/server не оставляет каналов или ожиданий. Возможности второго transport подтверждаются отдельно.

## Архитектура и решения

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
