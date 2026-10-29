# Core: план bootstrap и общих контрактов

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Этапы ниже запланированы; конкретные semantics уточняются по существующему API перед реализацией.

Уточнение2026-10-29: ProcessOutputCapture предоставляет отдельные read/file completion tasks для диагностики cleanup при прежнем borrowing contract. Ownership native streams и открытый timeout follow-up ведутся в [ARCH2e.4](../engine/PLAN.md#arch2e4-устойчивость-output-drain--follow-up-2026-10-29); новых App/DI scopes нет.

Решение2026-10-29 для [ARCH2e.4](../engine/PLAN.md#arch2e4-устойчивость-output-drain--follow-up-2026-10-29): file-enabled ProcessOutputCapture использует dedicated worker и прямой sync wake-up, чтобы после EOF drain не зависел от доступности shared ThreadPool. Queue8, UTF8/tail/file budgets, borrowing и failed-IO accounting сохраняются; один worker на capture — явная цена. No-file mode без worker. [Доказательство/границы](HISTORY.md#file-drain-без-shared-threadpool--2026-10-29).

## Границы

Дополнение2026-10-29 для ED6a.2: bounded `UseDiagnostics(options)` предоставляет
nondisposable `IDiagnosticHealthReader` с in-memory counters parent collector/sinks/
emergency/retention. Новый `ProcessOutputCapture.Read(maximumCharacters)` и
`ProcessOutputSnapshot.Limit` сохраняют capture/file counters и отдельно считают
display-window OmittedCharacters. Прежние signatures/owners/IO deadlines сохранены;
Core не получает зависимости от Aether/UI. [Semantics](DIAGNOSTICS.md).

Входят TrueMoon, TrueMoon.Contracts, TrueMoon.Core и TrueMoon.Tests. Bootstrap/App.Builder, configuration, diagnostics и app lifecycle находятся в TrueMoon; независимые публичные contracts — в Contracts, utilities/buffers/scheduler — в Core. Это группа планирования, не предложение объединить сборки.

DI resolver и generators относятся к [Cobalt](../cobalt/PLAN.md), IPC — к [Enerit](../enerit/PLAN.md), units/processes — к [Mithril](../mithril/PLAN.md), UI — к [Alloy/Argentis](../alloy/PLAN.md). Core определяет их общие app/service boundaries и не получает графические зависимости.

## Цель

Приложение без UI предсказуемо строится, читает конфигурацию, запускает сервисы, сообщает diagnostics, завершается по cancellation и освобождает принадлежащие ему ресурсы. Неудачный startup имеет наблюдаемую причину и проверяемый cleanup.

## Архитектура и решения

Решение2026-10-22 в рамках [Aether DI1–DI4](../engine/PLAN.md#приведение-к-правилу-cobaltifactory):
Contracts расширяет IFactory<T> типизированным IFactory<T,TArguments> и Factory
adapters без владения результатами. Legacy Create/Create<TData> сохранены; typed
factory требует корректные arguments. AppServiceLifecycle forwards start/stop,
не реализуя disposal целевого сервиса: DI aliases не создают дополнительных owners.
Новых зависимостей общей сборки на providers/modules нет.

Уточнение2026-10-21: TrueMoon.Contracts — общий пакет контрактов, базовых моделей
и интерфейсов для взаимодействия модулей; App contracts — часть этого назначения.
Специфичные для отдельного модуля типы остаются в своём модуле. Общая сборка
не зависит от конкретных реализаций/providers; внутри неё применяются общие
правила смысловых категорий и соответствия namespace пути.
[Аудит и выполненные исправления](../HISTORY.md#исправление-структуры-проектов--2026-10-21).

Сохранять App.Builder и IServiceResolver/IStartable/IStoppable/IAppLifetime boundaries, отделяя bootstrap от реализации DI. Текущий TrueMoon ссылается на Cobalt/его generator и Contracts; это зависимость существующей реализации, не изменение новым планом.

Правила повторного Start/Stop, cancellation, порядка запуска/остановки/rollback, configuration precedence/validation и diagnostics ownership уточнены исправлением2026-10-10 ниже. Их гарантии относятся к проверенным сценариям; наличие имён API само по себе не подтверждает semantics.

### Принятые контракты исправления ревью — 2026-10-10

Основание: запрос исправить выявленные проблемы ядра. Изменения сохраняют существующие app/service interfaces; следующий набор проверен component/integration tests на Windows/Debug/net10.0, подробности в [HISTORY](HISTORY.md#исправление-проблем-ядра--2026-10-10).

- `RunAsync` обоих entry points владеет приложением: Start → ожидание → Stopping → Stop → Stopped → Dispose. Ошибка передаётся caller; несколько ошибок сохраняются в AggregateException. Внешняя cancellation наблюдается как OperationCanceledException после cleanup; lifetime.Cancel завершает обычный run.
- Start/Stop сериализованы. Повторный Start работающего приложения и повторный Stop завершённого — no-op; restart после Stop запрещён. Startup failure откатывает только успешно запущенные stoppable services в обратном порядке; нормальный Stop включает отдельно зарегистрированные IStoppable. Повторный/concurrent Dispose ожидает одно завершение.
- Shutdown/rollback получают отдельный token с таймаутом30s. Ограничение кооперативное: сервис обязан учитывать token; принудительный обрыв произвольного пользовательского Task не гарантируется. Ctrl+C подавляет немедленное завершение и запрашивает lifetime cancellation; handler снимается при Stop/failure/Dispose.
- Lifetime callbacks используют snapshots и выполняются вне lock, ошибки не прерывают остальные notifications. Wait continuations асинхронны; owned CTS освобождается после активных cancellation callbacks, borrowed CTS остаётся у caller. Отмена borrowed source тоже завершает ожидание.
- Позже добавленный configuration provider имеет больший приоритет; в прямом CommonConfiguration входной порядок задаёт приоритет. Секции одного имени объединяются по ключу; Get/indexer/Exist согласованы. Existing null/invalid блокирует fallback; missing возвращает default, invalid Get<T> выбрасывает FormatException, TryGetValue возвращает false; conversion invariant. Named writes идут в старший provider, unqualified writes — в default.
- Refresh переснимает args/env и JSON; failed refresh сохраняет прежний snapshot. JSON filename задаёт section name, Set синхронно сохраняет файл, ошибки файлов/JSON наблюдаемы. GetOrCreate без имени использует имя типа. Paths разбирают точный root argument и metadata продукта.
- Diagnostics subscription и source factory принадлежат resolver; освобождаются общий и individual handles, sources. Ошибки observers изолированы; настройки и collections возвращают snapshots. Новые filters действуют при обнаружении источников, повторная привязка существующих listeners не вводится.
- Scheduler закрывает приём и выполняет уже принятые задачи до Completion; внешний Dispose ожидает workers, self-worker Dispose только запрашивает завершение. Consumers должны дождаться async operations перед закрытием scheduler, нужного их continuations.
- Writer возвращает массив один раз, очищает references и запрещает новые операции после Dispose. MemoryPoolUtils принимает только целую логическую rental region; foreign/partial/double/stale Return отклоняется. `Rent<T>` добавляет IMemoryOwner; Memory остаётся в public API, но теперь manager-backed и TryGetArray может вернуть false. Полученные ранее spans/pins нельзя использовать после возврата rental.

## Этапы

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| CORE0 | Исходная сверка runtime/test discovery и AppCreate historical failure | Отдельные результаты TrueMoon.Tests/configuration; существующий failure подтверждён либо явно отмечен невоспроизведённым |
| CORE1 | Зафиксированный app/service lifecycle и ownership/error contract | Создание/start/stop/cancel/repeat/startup failure/disposal имеют поведенческие проверки и согласованный порядок cleanup |
| CORE2 | Рабочие configuration providers и diagnostics | Документированные precedence/invalid values; подписки освобождаются, ошибки/события видны на контрольном сценарии |
| CORE3 | Интеграция выбранных DI providers | Один bootstrap/lifecycle сценарий на Cobalt и Microsoft DI adapter; различия capabilities отмечены явно |
| CORE4 | Общие utilities по потребности consumers | Buffers/scheduler, участвующие в контрольных сценариях, проверены на границах/lifetime; оптимизация только после измерения |
| CORE5 | Поставка и пример headless app | Independent consumer подтверждает запуск/shutdown/diagnostics; references и package metadata соответствуют фактическому артефакту |
| CORE6 | Структурированная диагностика — bounded профиль CORE6a–c, emergency/managed runs и первый operation профиль CORE6d; полный этап частично | [CORE6](#core6-структурированная-диагностика--2026-10-25): queues/sinks/lifecycle, tracing/operation metrics и Aether consumer; IPC propagation, aggregate metrics, bootstrap/routing/generation и release acceptance отдельно |

CORE0 предшествует выбору fixes. CORE1 и COB1/COB2 связаны через service ownership; lifecycle приложения остаётся задачей Core. CORE2/CORE4 развивать по потребности consumers, без неподтверждённого переписывания utilities.

### CORE5 / COB4: независимый headless consumer — 2026-10-11

Основной подробный сценарий этого совместного этапа ведётся здесь; Cobalt COB4
delivery уточнён2026-10-14: [два projects/один package](../cobalt/PLAN.md#принятый-scope-и-один-пакет--2026-10-14),
bundled direct/transitive analyzer и opt-out вместо standalone generator reference.
Исторический2026-10-11 explicit analyzer contract ниже сохранён как описание того
run; current consumer и повторные64+3 checks используют новую поставку. Cobalt
ссылается на него. [Исходники и воспроизведение](../../ManualTests/CorePackageConsumer/README.md),
[команды и результаты](HISTORY.md#core5--cob4-headless-package-consumer--2026-10-11).

- Только PackageReference и собственные исходники consumer; отдельный local feed,
  уникальная prerelease версия, изолированный cache и проверенные source metadata.
- Headless App на Cobalt/Microsoft DI: configuration, diagnostics, normal lifetime
  completion, external cancellation, startup/stop/build failure, cleanup и исходные
  ошибки; Debug/Release × build/publish без RID.
- COB4 дополнительно проверяет actual compiler analyzer path/SHA256, consumer-generated
  ModuleInitializer и Composite/open/closed generic graph; direct Cobalt без App/adapter.
- Генератор подключается явно отдельным `TrueMoon.Cobalt.Generator` PackageReference
  с `PrivateAssets=all`. Runtime packages не обещают его автоматической доставки.
  Microsoft DI consumer проверяется также без generator; обязательная Cobalt generation
  проверяется отрицательной сборкой без analyzer package.
- Package README примеры извлекаются из собранных артефактов, компилируются и запускаются.
  Owned/borrowed и provider-specific repeated disposal сохраняют принятые контракты.

Этап проверен на Windows x64/net10.0/SDK10.0.401; это bounded package proof,
не общий release, UI/process/IPC evidence, trimming/NativeAOT или other compiler hosts.
Дальше для Core/Cobalt — COB5 startup/resolution/allocations baseline на этом graph;
ближайший общий функциональный шаг — bounded own-child MIT0/ENE2 scenario.

## Проход качества до engine/editor — 2026-10-15

После Cobalt COB6/COB5/COB7 выбран собственный проход Core. Существующие
CORE0–CORE5 и их исторические проверки сохраняются; следующий объём ограничен
configuration/bootstrap, diagnostics и контрольным headless приложением.

| Часть | Объём и критерий | Состояние |
| --- | --- | --- |
| Configuration/bootstrap | Приоритеты/null/invalid, GetOrCreate, неудачный refresh нескольких providers, JSON write failure/retry; configuration actions заканчиваются до registrations/factories на обоих DI providers | Проверено2026-10-15 в component/App tests; детали в HISTORY |
| Diagnostics | Время жизни subscriptions при завершении sources, повторная настройка и независимое освобождение нескольких приложений | Проверено2026-10-15:18 diagnostics cases, Core107 Debug/Release; детали в HISTORY |
| Сквозное приложение | Configuration + diagnostics + Cobalt, normal/cancel/startup failure/shutdown и cleanup; свести ограничения перед следующим модулем | Проверено2026-10-15:52source +80package +3README processes, JSON и реальный файловый ресурс |

Уточнение GetOrCreate: existing non-null возвращается без записи, invalid вызывает
FormatException без fallback. При existing null новый объект записывается в секцию,
которая определила это значение; это явная операция изменения, включая запись JSON
файла. Для отсутствующего unqualified key остаётся default section, для named —
выбранная секция. Обычный Set без имени секции по-прежнему пишет в default.
Это исправляет повторное создание объектов из-за маскирующего null в другой секции;
конкурентный GetOrCreate и межпроцессная синхронизация этим контрактом не вводятся.

Новые providers, typed binding/validation API, автоматический reload, file transactions,
новые lifecycle/DI capabilities и performance optimizations в этот проход не входят.
[Доказательства и команды](HISTORY.md#configuration-и-bootstrap-quality--2026-10-15).

Уточнение diagnostics2026-10-15: завершение одного DiagnosticListener удаляет
только его subscription по identity; существующие и новые источники с тем же
именем продолжают работать. Subscribe/handle attachment выдерживает completion
до возврата handle и уже закрытый source без OnCompleted. Общая подписка
освобождает оставшиеся handles; unsubscribe допускается из event callback.
Dispose не обещает дождаться callback, уже исполняющегося на другом потоке.

Повторный UseDiagnostics заменяет прежние settings и registrations подписки/factory;
последний вызов определяет конфигурацию, ResolveAll не создаёт старые подписки.
Настройки разных Apps независимы, закрытие одной App не отключает другую.
Источники DiagnosticListener остаются глобальными в процессе: каждый App получает
все события, подходящие его prefix filters, включая источники другого App.
Это независимость ownership, без нового app-specific event routing/privacy API.
Filters по-прежнему применяются при discovery без rebinding старых источников;
EventsSourceFactory сохраняет ownership своих источников до своего Dispose.
[Diagnostics evidence](HISTORY.md#diagnostics-quality--2026-10-15).

### Сквозной headless proof — 2026-10-15

[Воспроизводимый runner](../../ManualTests/CoreHeadlessProbe/README.md) использует
общие исходники consumer через ProjectReference и независимые PackageReference.
Семь сценариев Cobalt: normal lifetime, external cancellation, startup failure,
stop failure, build failure, configuration validation failure и startup+Dispose
failure. Microsoft DI adapter имеет6compatibility controls без расширения его
native Dispose-error semantics. Debug/Release × build/publish, без RID.

JSON null replacement/persistence, provider priority и failed refresh/recovery
проверяются вместе с сервисным графом. Реальный эксклюзивный FileStream остаётся
открытым во время Start/Stop и повторно открывается после cleanup; при validation
failure не создаётся. Ошибки сохраняют identity/token, Cobalt сохраняет обе причины
startup/cleanup и продолжает Dispose остальных объектов. Borrowed instance остаётся
caller-owned. Observable diagnostics/observer isolation, повторный setup без старых
subscriptions и отсутствие доставки после shutdown проверяются в том же процессе.

Согласованный bounded Core quality проход закрыт на Windows x64/net10.0; это
подтверждение текущих contracts/component/runtime/package путей, не общий выпуск
или доказательство engine workload. Новых public APIs и runtime fixes этим
headless шагом не понадобилось. Ограничения в STATUS сохраняются: cooperative
shutdown, process-global diagnostics, JSON write/reload/concurrency, other platforms/
deployment modes, warnings и отдельные performance задачи. Далее Enerit quality
review и ENE5 measured IPC baseline по его плану. Перед ENG0 нужна сводка остальных
модулей; Core proof не завершает Enerit/Mithril/UI и не запускает engine.
[Команды и результаты](HISTORY.md#сквозной-headless-proof--2026-10-15).

## CORE6: структурированная диагностика — 2026-10-25

Согласовано пользователем: развивать собственный TrueMoon diagnostics API, сохраняя
путь подключения через `UseDiagnostics` и совместимость существующих consumers.
`Microsoft.Extensions.Logging`, его адаптер и обязательные внешние collectors пока
не требуются и в этот этап не входят. Это целевой roadmap; первый реализованный
профиль описан ниже, остальные задачи не становятся автоматически завершёнными.

### Модель и границы

- Общие contracts и базовые значения размещать в TrueMoon.Contracts; реализацию
  pipeline — в TrueMoon. Domain event definitions/payloads остаются у своих модулей.
  Публичные имена/signatures и способ эволюции IEventsSource/DiagnosticEvent уточнить
  перед реализацией; legacy Trace/Message/Exception и OnEvent требуют migration contract.
- Event envelope: UTC timestamp, source, уровни Trace/Debug/Information/Warning/Error/
  Critical, устойчивые EventId/EventName, message template и structured properties,
  exception отдельным полем, App/process/operation context и trace/span correlation.
  Caller — дополнительное поле, не identity события. Sequence задаёт порядок внутри
  источника/процесса; длительности измерять монотонно. UTC не обещает общий причинный
  порядок событий разных процессов.
- Перед asynchronous delivery фиксировать immutable bounded snapshot: определить
  допустимые значения, число/размер/глубину properties, размер message/exception и
  явные truncation markers. Не удерживать произвольные application objects/Views
  и не сериализовать arbitrary payload graph. Правила исключения чувствительных
  данных и отображения exception согласовать вместе с форматом.
- Разделить log events, operation traces через ActivitySource и numeric metrics
  через Meter. Frame/request подробности включать ограниченным diagnostic capture;
  обычные показатели агрегировать. Не использовать session/request IDs как
  неограниченные metric dimensions. Отключение сбора не меняет application outcome.

### Операции CORE6d — 2026-10-26

Реализован первый listener-driven профиль: общие IDiagnosticTelemetry/Operation
contracts, App-owned ActivitySource/Meter, monotonic duration и counter.
Имена операций ограничены64по умолчанию; metric dimensions только operation/outcome.
Lazy sampled trace properties, async parent context, exactly-once completion,
observer failure isolation и deferred release активных scopes описаны в
[guide](DIAGNOSTICS.md#трассы-и-метрики-операций).
Aether использует профиль для Build/Replace/preview/frame faults.
26октября [Enerit pipes profile](../enerit/DIAGNOSTICS.md) использует новый совместимый
kind/parent overload IDiagnosticTelemetry для Client/Server и explicit remote parent.
CORE6d остаётся частичным: дополнительные IPC/command и frame aggregate metrics,
экспорт/long-load и остальные domain producers ещё требуются. Новые exporters
и Microsoft.Extensions.Logging не добавлены.

### Подэтапы

| ID | Задача | Критерий завершения |
| --- | --- | --- |
| CORE6a | Event schema, context и совместимость API | Типизированные события сохраняют отдельные exception/properties, устойчивые IDs и lazy creation; определены legacy mapping, App routing и совместимость с process-global DiagnosticListener. Domain state не восстанавливается парсингом логов |
| CORE6b | Early filtering и bounded delivery | Фильтры по source/level/event/sink проверяются до payload creation; runtime level changes действуют для существующих sources. На producer/UI/render thread нет sink IO/форматирования или ожидания свободного места. Явные queue/event byte budgets, overflow policy, приоритет ошибок и counters потерь; медленный/ошибающийся sink не задерживает остальные |
| CORE6c | Sinks, retention и App lifecycle | Console, rotating JSONL files и bounded in-memory ring; per-file/total disk/count/age и memory limits заданы до запуска. Sink health, dropped/truncated counters и ограниченный emergency output доступны без рекурсивного логирования. Start/stop/drain/dispose, failed startup/cleanup и повторный setup имеют одного owner через App/Cobalt/IFactory |
| CORE6d | Operation tracing и metrics | ActivitySource/Meter связаны с log context; parent/child операции, async flow и disabled/sampled collection корректны. Межпроцессная передача context реализуется transport-владельцем, Core не получает Enerit/Aether зависимости |
| CORE6e | Типизированные event methods и generation | После стабилизации CORE6a и первого consumer определить декларации событий/генерацию, проверки duplicate IDs/templates/property types и доставку analyzer. Измерить disabled/enabled allocations и throughput; не обещать zero allocation без измерения. Генератор не prerequisite первого bounded consumer |

UseDiagnostics фиксирует options и регистрирует pipeline/sinks/factories; сервисы
заимствуют injected source, не ищут resolver на каждом событии. До реализации
определить начало захвата bootstrap failures, порядок завершения producers → drain
→ dispose, общий конечный shutdown budget и late-write outcome. Логирование ошибок
cleanup должно оставаться доступным до закрытия владельцев; worker/sink resources
нельзя освобождать при ещё исполняющемся IO. Ошибки/таймаут drain наблюдаемы,
ownership для retry явно определён. При abrupt process death хвост async queue
может быть потерян; гарантии полной доставки и fsync этим планом не заявляются.

### Первый срез и acceptance

Сначала CORE6a–c и минимальная связь с activity context для
[Aether ENG8a](../engine/PLAN.md#eng8a-диагностика-сессий-и-процессного-вывода--2026-10-25),
затем расширение traces/metrics и typed generation по consumer. Численные budgets
фиксировать перед кодом и проверять на воспроизводимом workload.

Нужны headless scenarios: disabled filtering без вызова payload factory; concurrent
producers; slow/throwing sink; oversized/mutable payload; queue overflow с точными
loss counters; file rotation/retention и disk/write failure; независимые Apps и
повторный setup; startup failure и shutdown с pending events/failed drain/retry.
Подтвердить ограниченность памяти/диска/времени ожидания и доступность исходной
ошибки caller. Cobalt — основной graph, существующий Microsoft DI adapter сохраняет
свой regression scope. Package/consumer proof относится только к реально запущенным
конфигурациям; прежний CORE2 proof не подтверждает новый CORE6.

### Первый bounded профиль CORE6a–c — 2026-10-25

Реализованы DiagnosticLevel/Message/Record, совместимый Write/IsEnabled,
opt-in UseDiagnostics(options), live source/event level gates и sink thresholds,
frozen bounded snapshots с Activity IDs, независимые очереди/health, memory ring,
console и rotating UTF8 JSONL. Legacy OnEvent остаётся синхронным, global listener
routing сохраняется; ApplicationId обозначает collector, не producer-private App.
[Подключение, numerical defaults и lifecycle](DIAGNOSTICS.md).

Default: queue256/4MiB estimated bytes включая in-flight, reserve16для Error;
memory512/4MiB estimated, event message2048char/exception fields4096char,
properties≤16×256char, names128char. JSONL≤4×1MiB на prefix/directory, exclusive
owner и reuse/shrink slots; Dispose wait5s с сохранением ownership при timeout.
ProcessOutputCapture в Core непрерывно читает borrowed TextReader, tail32KiChars,
UTF8 file≤1MiB через отдельную bounded8chunk queue; newline не требуется.

Current Core179/179 Debug/Release:62новыхcases (43pipeline/19capture) плюс117прежних;
full solution3725/configuration и Aether7новыхcases подтверждают первый consumer.
[Команды и evidence](HISTORY.md#структурированная-диагностика-первый-профиль--2026-10-25).
Emergency output и age/total directories retention дополнены следующим срезом.
Остальной CORE6: bootstrap-before-resolver capture, App-private routing, spans/metrics,
IPC propagation и generation требуют следующих срезов. Byte accounting memory —
estimate; measured allocations/throughput и независимые NuGet consumers не повторены.

### Emergency и managed JSONL runs — 2026-10-26

CORE6c дополнен независимым конечным emergency worker, default borrowed
Console.Error, queue8 включая executing и16attempts на жизнь pipeline,
report≤1024chars вместе с одним LF. В него не переносятся payload/exception
objects/messages; writer failure только увеличивает health, не пишет сам в себя.
Normal sink/capture/startup/dispose failures, lost Error/Critical, retention
limitations и best-effort shutdown timeout наблюдаемы без обычного log routing.
Sink cleanup и emergency drain имеют общий ShutdownTimeout, timeout сохраняет
исполняющееся IO до завершения/retry; borrowed writer не освобождается.

JsonUseRunDirectories включает GUID run под постоянным JsonDirectory root.
Exclusive versioned marker удерживается до закрытия файлов; shared root lease
согласует cleaners. JsonRetention задаёт closed runs count/total bytes/age,
default8/32MiB/7суток от creation UTC. Equality допускается; most recently created
closed run и явно защищённый current сохраняются даже сверх budget с health flag.
Удаление нерекурсивное, только known flat events files/locks; active runs,
legacy/unknown directories и reparse entries сохраняются. Active runs имеют
отдельный per-run file budget; total artifact/report root этим не ограничен.
Sweep на first write/dispose, без фонового таймера. Flat API остаётся совместимым.
[API, numerical bounds и ownership](DIAGNOSTICS.md).
Editor использует постоянный parent logger root между запусками; Runtime —
managed root в report directory, отдельно от artifacts/raw output.
[Проверки и оставшиеся границы](HISTORY.md#emergency-и-managed-jsonl-runs--2026-10-26).

### Bounded history reader/importer для child delivery — 2026-10-27

Contracts добавляет IDiagnosticRecordReader (LatestSequence/ReadAfter) и
IDiagnosticRecordImporter.TryImport. Pipeline предоставляет nondisposable views
через UseDiagnostics(pipeline), чтобы Cobalt/MS DI aliases не освобождали owner
повторно. Legacy UseDiagnostics снимает views вместе с pipeline settings.
DiagnosticRecord получает additive init Origin(child collector appid/sequence),
его прежний positional constructor не меняется. Импорт назначает host sequence/
appid, сохраняет emitting PID/time/trace/exception strings, refreeze/clip и current
filter/queue policies. Authoritative context копируется первым в том же property
budget. Already-imported/self/invalid origins отвергаются, global events не
re-emit; read snapshots ограничены1..1024 и не передают ownership.
Domain transport/child identity остаётся в
[Aether](../engine/PLAN.md#child-diagnostic-delivery-eng8a3--ene6d4--2026-10-27).
[Реализация и проверки27октября](HISTORY.md#чтение-и-импорт-child-diagnostics--2026-10-27).

## Критерии готовности

- Известный набор тестов действительно обнаружен и пройден, skipped/failed указаны; tests existence не считается passing результатом.
- Startup failure, cancellation и shutdown не оставляют принадлежащие app сервисы/подписки; ошибки доступны caller по согласованному контракту.
- Configuration и diagnostics работают вне UI и не требуют renderer/window.
- Согласованный bootstrap сценарий работает на заявленном DI provider; неподдержанные provider semantics описаны.
- Build/package consumer и пример воспроизводятся с указанными SDK/TFM/packages; публикация UI не заменяет этот consumer.

## После первой рабочей проверки

Расширять configuration/diagnostics по реальным приложениям; измерять startup и allocations перед оптимизацией. Новые providers и дополнительные lifecycle API вводить отдельным решением. Параметры общего выпуска — в G/R критериях [общего плана](../PLAN.md).
