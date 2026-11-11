# Core: план bootstrap и общих контрактов

<a id="current-core-queue"></a>
## Точка продолжения — актуализация 2026-11-11

Ближайшая работа по запросу пользователя — [CORE8: восстановление solution на .NET11](#core8-dotnet11).
CORE8.0 и CORE8.1 завершены в managed build-input/profile scope: все 61 проекта
net11.0, общий SDK/TFM profile для 45 runners, исправленный внешний CPM restore,
ordinary source build D/R и 16-package App/child delivery.
[Результаты/границы](HISTORY.md#core81-profiles-2026-11-11).
CORE8.2 завершён: Cobalt179/generator96 и Enerit354/generator114 каждый D/R,
132independent package DI/IPC scenarios; production API/dependencies сохранены.
[Точные результаты](HISTORY.md#core82-generators-2026-11-11).
CORE8.3 завершён: full Aether3243/Editor1496 каждый D/R, actual frozen game
source/published compile/reuse/load/Stop/reap на SDK11-only root.
[Результаты/границы](HISTORY.md#core83-frozen-2026-11-11).
CORE8.4 завершён: [полная регрессия](HISTORY.md#core84-runtime-2026-11-11). CORE8.5 завершён: [согласованная поставка и новые полные suites D/R](HISTORY.md#core85-delivery-2026-11-11). Сквозной CORE8 закрыт в согласованном Windows x64 scope; функциональная очередь ARCH1f.4 и отдельные Core/diagnostics задачи сохраняются.

CORE7.0–7.6 завершены на прежнем net10 профиле: [guide](CONFIGURATION_GUIDE.md),
[139keys/17schemas](CONFIGURATION_KEYS.md), [App/child delivery](../../ManualTests/ConfigurationDelivery/README.md).
[Предыдущие6945cases/D/R и16package proofs](HISTORY.md#core76-2026-11-10)
не подтверждают текущие net11 inputs. Новые настройки подключать через общий слой.
CORE6 routing/metrics/quota, engine f.4 и wider physical/release gates сохранены;
следующая общая проверка сборки предшествует продолжению feature работы.

CORE0–5 и bounded CORE6 сохраняют исторические proofs. App-private/bootstrap
diagnostic routing, aggregate metrics CORE6d/generation CORE6e и artifact quota
остаются самостоятельными задачами. Editor console/export/child health уже имеют
consumer; повторный post-reap drain требует causal evidence у engine owner.
Общий engine/UI порядок — [roadmap](../PLAN.md#current-roadmap).

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
| CORE7 | [Единая конфигурация App и всех модулей](#core7-unified-configuration), запланировано 2026-11-09 | Общие источники, ключи, приоритеты, parser, typed options, bootstrap и снимки; поэтапная миграция и проверка поставки. Сейчас готовы только план и инструкции |

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

### Bounded archive/configuration views для ED8a — 2026-10-30

CORE6c дополнен borrowed `IDiagnosticArchiveReader` и
`IDiagnosticConfigurationReader`. Первый читает recognized JSONL tails с отдельными
IO/retained/line/file/time budgets и explicit omissions; второй копирует effective
scalar settings без путей, rule names и callbacks. App остаётся единственным
владельцем pipeline, consumer отменяет/drain свой archive request; sink flush и
filesystem maintenance не запускаются. Подробный контракт —
[в guide](DIAGNOSTICS.md#архив-jsonl-и-действующие-настройки),
[проверки](HISTORY.md#архив-jsonl-и-конфигурация-для-ed8a--2026-10-30).
Полный CORE6, App-private routing, aggregate metrics и release matrix остаются
отдельными задачами. UI/report privacy policy ведётся только в Editor плане.

<a id="core7-unified-configuration"></a>
<a id="core7-единая-конфигурация-app-и-всего-фреймворка--запланировано-2026-11-09"></a>
## CORE7. Единая конфигурация App и всего фреймворка — завершено 2026-11-11

Основание: запрос пользователя унифицировать Args/EnvironmentVars, настройки App
и их использование во фреймворке, закрепить это в инструкциях. Владелец общего контракта — Core
(TrueMoon.Contracts/Configuration и TrueMoon/Configuration/App bootstrap).
Модульные adapters/typed options остаются у владельцев модулей. Это единственный
детальный план задачи; общая и модульные очереди ссылаются на него. Исторические
проверки CORE2 сохраняются; CORE7.0–7.6 завершены в описанном configuration scope. [Исходная сверка и решение](HISTORY.md#configuration-unification-plan-2026-11-09).

**2026-11-10 — CORE7.0 завершён как проектный этап.** [Карта использования](CONFIGURATION.md)
и [точные сигнатуры/semantics API](CONFIGURATION_API.md) готовы. Введены проектные
границы legacy/unified reader, optional App stage, schemas/codecs/provenance,
snapshots/reload/write. Declarations + три consumer examples прошли Release type-check:
0 errors/0 warnings, SDK10.0.403, без выполнения заглушек и production rebuild.
[Запись проверки](HISTORY.md#core70-api-2026-11-10). Статическая карта 833 production
файлов/6 args-env calls в5файлах сохраняет область сверки; код не мигрирован.

**CORE7.1 реализован и проверен 2026-11-10.** Opt-in standalone profiles,
schemas/defaults/codecs, normalized paths, explicit priorities, CLI/env/JSON и
immutable raw snapshots; старые API не менялись. Уточнение metadata: optional
Key.Codec/type-erased codec передаёт custom converter публичным schemas; поздние
App/reload/write interfaces не публикуются как неработающие services.
Core506/506 D/R;108newcases, source Debug/published Release/fresh3packages по15checks.
Solution обычный incremental Debug0errors/236warnings, не full-solution tests.
[Evidence](HISTORY.md#core71-2026-11-10), [usage](CONFIGURATION_USAGE.md).

**CORE7.2 реализован и проверен 2026-11-10.** [App-owned stage/reader](CONFIGURATION_APP.md):
all schemas bind/configure/validation/copies до module register/DI, optional catalog,
borrowed nondisposable alias и independent snapshots. Cobalt/MS DI, legacy ordering,
two Apps/repeated Build, failed creation/startup/disposal проверены; реальные
две Mithril MainProcess declarations согласованы после всех register callbacks.
Core554/554 D/R,48newcases; source/published/fresh6packages по36checks/profile.
[Scope](HISTORY.md#core72-2026-11-10). Нет reload/write или автоматического cutover.

### Цель и исходная точка

Один и тот же логический параметр получает одинаковое имя, тип, приоритет и ошибку
через CLI/env/JSON и публичную настройку модуля; потребители получают проверенные options
через DI. Стандартом остаётся собственный контракт конфигурации TrueMoon, общий
для Cobalt/MS DI; переход на Microsoft.Extensions.Configuration или новые зависимости
не назначены этим решением.

Текущее поведение, сверенное 9 ноября: App автоматически создаёт args/env/default;
Get<T>(key) ищет по provider order, GetSection() без имени выбирает default; AddProvider
вставляет в начало, поэтому произвольный поздний JSON provider может перекрыть CLI.
Args поддерживает raw key=value, сохраняет префиксы, не объединяет --key value;
env копирует все process variables с исходными именами. JSON opt-in и mutable Set/
GetOrCreate могут синхронно писать файл. Часть typed module options настраивается
callbacks отдельно, Editor имеет собственный CLI parser, RuntimeBootstrap читает
env до App, Mithril использует общий IConfiguration для -u/-p/control pipe.

### Целевые решения и ограничения

Следующие правила — принятый ориентир реализации. Точные новые сигнатуры C#,
форматы схем/aliases, профиль совместимости и способ регистрации фиксируются
в CORE7.0 до изменения публичного API. Новые методы Bind/Options/Bootstrap
пока не реализованы.

1. **Один путь настройки и явный порядок.** Приоритет от низшего к высшему:
   значения модуля по умолчанию → подключённые JSON/файлы → env → args → явные
   программные переопределения/configure callbacks. Все переопределения применяются
   до окончательной проверки и активации сервисов. Для значения известны источник,
   раздел и ключ. Приоритет provider задаётся явно и не зависит от случайного
   порядка AddProvider. Файлы подключаются по явным App paths с признаком
   обязательности; поиск по произвольному рабочему каталогу не выполняется.
2. **Единое адресное пространство.** Логические ключи включают модуль/раздел:
   `App:Name`, `Editor:ProjectPath`; сравнение — `OrdinalIgnoreCase` без зависимости
   от текущей культуры. Get/indexer/Exist/TryGet/чтение разделов/binding согласованно
   возвращают итоговое значение. Корневой поиск не перебирает одноимённые листья
   несвязанных разделов. Чтение исходного provider и объединённого корня различаются
   явно; старый GetSection/default API переводится через профиль совместимости.
3. **Общий CLI parser.** Поддержать `--key=value`, `--key value`, объявленные короткие
   aliases, логические флаги, отрицательные числа, позиционные аргументы и разделитель
   `--`. Нормализация префиксов и aliases едина для всех приложений и модулей.
   Для повторного скалярного ключа побеждает последнее значение; упорядоченные
   коллекции объявляются схемой, без неявного разделения по запятой. Отсутствующее
   значение, неизвестная опция и конфликт aliases дают явную диагностику. Описать
   переход с `-u/-p/-ud/-root` и `key=value`. Parser принимает явный `string[] args`
   и работает без App/DI.
4. **Предсказуемый env provider.** Настраиваемый префикс приложения/список разрешённых
   ключей; двойное подчёркивание обозначает раздел: `TRUEMOON_EDITOR__PROJECTPATH`
   → `Editor:ProjectPath` при префиксе `TRUEMOON_`. Одиночные подчёркивания и посторонние
   переменные ОС не переименовываются по догадке. Неоднозначные имена/aliases после
   нормализации отклоняются. Старое чтение env доступно через ограниченный адаптер
   совместимости. Полное окружение не публикуется в диагностике.
5. **Типизированные options и проверки.** Значения по умолчанию → общий binding →
   явный callback модуля → проверка допустимости → копия/неизменяемые options в DI.
   Поддержать вложенные значения, коллекции, enum и преобразование без зависимости
   от культуры. Различать отсутствие/null/пустоту/ошибку; недопустимое значение
   победившего источника не скрывать чтением младшего. Ошибка содержит ключ, тип
   и источник, чувствительное значение маскируется. Внутренние экземпляры сервисов,
   delegates и `IDiagnosticsConfiguration` переводятся к типизированной регистрации
   DI; для прежнего хранения объектов в конфигурации нужен адаптер совместимости.
6. **Bootstrap и граница процесса.** Общие parser/нормализация/проверки работают
   до App; второй App или service locator для разбора args не создаётся. Модули
   сохраняют типизированные launch/transport contracts. Parent/session/nonce/protocol
   identity читаются отдельным защищённым bootstrap profile и не переопределяются
   пользовательскими CLI/env/файлами. Данные дочернего процесса собираются из typed
   launch data; проверки identity/размера/версии/ownership Aether/Mithril сохраняются.
   Корневые пути и пути конфигурации определяются через общий bootstrap без цикла DI.
7. **Снимки, перечитывание и запись.** По умолчанию потребитель получает проверенный
   снимок; окружение не перечитывается на каждый Get/кадр. Явный reload собирает
   и проверяет полный кандидат, затем атомарно публикует новую версию; при ошибке
   остаётся прежняя. Настройки запуска требуют restart; динамические объявляются
   явно и обновляются через reader/уведомления с определённым владельцем и cleanup.
   Автоматическое наблюдение за файлами не обязательно для первой версии. Чтение
   и изменение итоговой конфигурации не записывают env/CLI/JSON: сохранение — отдельная
   явная операция. Новые гарантии сохранности при потере питания или транзакций
   с произвольными внешними писателями в эту задачу не входят.

### Этапы, consumers и критерии

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| CORE7.0 | Завершён проектный этап, 2026-11-10: [карта](CONFIGURATION.md), [API](CONFIGURATION_API.md) | Источники/consumers/legacy keys/options и точные добавочные signatures/semantics зафиксированы без новых обязательных членов legacy interfaces. Declarations + headless App Cobalt/MS DI, Mithril и Editor/Aether child examples скомпилированы Release0errors/0warnings; runtime/production proof не заявлен. Далее CORE7.1 |
| CORE7.1 | Реализован и проверен, 2026-11-10: [standalone profile](CONFIGURATION_USAGE.md) | Normalized keys/sections/приоритеты, schema metadata/defaults/codecs, общий CLI parser, prefix env и explicit JSON; source pairs/collisions/null/invalid/copies/cancellation покрыты. Core506/506 D/R,108newcases; source/published/fresh3package consumer по15checks. App stage ещё не реализован; далее CORE7.2 |
| CORE7.2 | Реализован и проверен, 2026-11-10: [App stage/reader](CONFIGURATION_APP.md) | All-schema deferred bind/configure/validation и private copies до register/DI, optional catalog, borrowed reader. Core554 D/R,48newcases; source/published/fresh6package consumer36checks/profile на Cobalt/MS DI с2MainProcess units. Legacy/new конфигурации отдельны; reload/write добавлены в CORE7.3 |
| CORE7.3 | Реализован и проверен, 2026-11-10: [reload/changes/writer](CONFIGURATION_RELOAD.md) | Atomic raw+prepared snapshots, rollback/cancellation/no-op/startup-only restart rejection, bounded64subscriptions/1pending и cleanup. Explicit JSON patch/delete/null, no implicit reload/write, shared runtime gate и IO retry. Core619 D/R,65newcases; source/published/fresh6package consumer72checks/profile на Cobalt/MS DI; нет новых interprocess/durability/native guarantees |
| CORE7.4 | Реализован и проверен, 2026-11-10: [Core/Mithril migration](CONFIGURATION_CORE_MITHRIL.md) | Standard Core name/paths/diagnostics data, DI activation, common selected bootstrap parser, original-provider protected typed launch. Legacy -u/-p/-ud/-root/appName retained via explicit adapters, process lifecycle unchanged. Core661 и Mithril155 D/R,86newcases; source/published/fresh6packages108checks/profile на обоих DI с real child graceful stop/reap; Mithril→TrueMoon dependency/API reviewed |
| CORE7.5 | Реализован и проверен в configuration scope, 2026-11-10: [модульный guide](CONFIGURATION_MODULES.md) | Aether/Editor/ECS/render/authoring, Enerit pipes, Nom и Hosting используют общие схемы и deferred binding; resources/identity/captures отдельно. Editor CLI — общий parser; pre-App runtime flags/parent frozen, старые aliases/callbacks сохранены. Neutral Contracts helpers не вводят bootstrap dependency в Editor/Hosting/Nom/Enerit.110newcases; source/published/fresh16packages76checks/profile, оба DI. Full Aether deviations и native limits отдельно в HISTORY |
| CORE7.6 | Завершён и проверен, 2026-11-11: [guide](CONFIGURATION_GUIDE.md), [keys](CONFIGURATION_KEYS.md), [delivery](../../ManualTests/ConfigurationDelivery/README.md) | Source solution D/R0errors;7suites6945/6945 D/R, включая full Aether3234.16fresh packages одной версии,5consumers×3forms, по230checks/4owned children на source/published/fresh profile; Cobalt/MS DI,0ProjectReference/fresh, payload/runtime hashes и metadata проверены.4API doc blocks compiled, guide example executed;8managed surfaces unchanged. Ordinary nested restore/старые reliability причины и native/GPU/release gates не объявлены исправленными |

Исторический порядок согласования: CORE7.0 → CORE7.1 → CORE7.2. Контракт снимков/записи определить в CORE7.0,
реализовать в CORE7.3 до перехода потребителей, которым нужен reload. CORE7.4/5
можно выполнять параллельно в разных модулях по готовому общему контракту.
Этот порядок сохранён как история; все этапы CORE7.0–7.6 выполнены. Профиль совместимости
и затронутые версии/пакеты определяются в CORE7.0; общая версия автоматически не меняется.

Готовность options/provider API и завершение миграции учитываются отдельно.
Первичная запись CORE7 меняла документацию; реализация и новые проверки перечислены по этапам выше. Текущий engine f.4 остаётся независимым направлением работы.

<a id="core8-dotnet11"></a>
## CORE8: восстановление solution на .NET 11 — план 2026-11-11

Основание: пользователь установил .NET 11 и начал перевод общей сборки.
Владелец сквозного плана — Core/build integration; адресные исправления остаются
в своих модулях. Это единственный подробный план миграции; общий roadmap ссылается
сюда. CORE7 завершён на прежних inputs, его результаты не подтверждают net11.0.
[Исходная сверка и команды](HISTORY.md#core8-planning-2026-11-11).

### Цель и границы

Штатные restore, build и тестирование TrueMoon.slnx работают с SDK11.0.100,
net11.0 и начатой пользователем версией0.3.0.1-alpha. Editor/Runtime действительно
собирают и загружают игру на том же профиле; fresh packages имеют согласованные
runtime/analyzer/native assets и запускаются независимым потребителем.
Первичная acceptance — Windows x64, Debug/Release, framework-dependent source,
published Release и isolated NuGet consumer. Другие OS/RID/AOT — отдельный scope.

Исходные пользовательские правки сохраняются: global.json, Directory.Build.props,
Directory.Packages.props, два benchmark csproj и два Example csproj. Смена TFM,
версии пакетов и runtime async учитывается раздельно. Не включать новую engine
заявку игры без отдельного принятия, не переписывать старые результаты net10.0.
Массовый API refactoring, новые graphics возможности и смена xUnit2 на xUnit3/MTP
не назначаются для восстановления сборки.

### Подтверждённые входные условия

- Установлены SDK11.0.100 и Microsoft.NETCore.App11.0.0; global.json выбирает
  стабильный SDK с allowPrerelease=false и rollForward=latestMinor.
- TF/TestsTF=net11.0, Version=0.3.0.1-alpha, LangVersion=latest,
  Features=runtime-async=on. Evaluation App, Cobalt.Generator и Core tests
  подтвердила эти значения, без restore/compile/test.
- В solution46проектов; в Git61csproj, из них11test projects. Ещё11manual/consumer
  csproj явно задают net10.0; AetherPackageConsumer/Consumer имеет отдельный props
  с тем же TFM. Часть этих проектов отсутствует в solution.
- В79tracked code/build/script files найдены net10.0/NET10/10.0.100. Это список
  для классификации, не79доказанных дефектов: negative/compatibility fixtures
  могут намеренно использовать старый TFM.
- Aether Builds ограничивает SDK major10, передаёт net10.0 в restore/build,
  проверяет его в input snapshot, lock target и artifact manifest.
- CPM сохраняет DotNetCliToolReference dotnet-xunit2.3.1. Предыдущий Aether proof
  требовал test-only props override; ordinary nested restore ещё не исправлен.
- Microsoft.Extensions.DependencyInjection уже11.0.0, Test.Sdk18.11.1;
  Roslyn5.9.0, xunit2.9.3/runner4.0.1 требуют проверки с новым compiler host.

### Решения миграции

Основной новый профиль — net11.0. Поддержка старого net10.0 не обещается сменой
SDK; необходимость multi-targeting оформлять отдельным решением. Новые game
artifacts обязаны согласованно объявлять net11.0; прежние cached net10 artifacts
не принимать за совместимые: явно диагностировать и пересоздавать. В CORE8.3
проверить необходимость изменения format/schema/cache identity до правки кода.
Сохранить hashes/frozen inputs, compiler policy, bounds и process ownership.

Runtime async, включённый пользователем, остаётся целевым режимом. Проверить его
применимость ко всем inheriting projects, Roslyn generators и runtime-compiled
game assemblies, передачу Features в compiler snapshots и diagnostics. Отдельный
контроль с runtime-async=off нужен для локализации отличий codegen от смены SDK;
он не заменяет acceptance целевого on и не меняет общий default без объяснения.
Не обещать ускорение без измерений. Не перезаписывать чужие compiler features
при условной настройке и не включать EnablePreviewFeatures автоматически.

Общие TF/TestsTF остаются источником для workspace projects; независимые consumers
получают явный согласованный TFM/SDK. Пути bin/obj/lib и проверки nupkg получать
из выбранного/evaluated профиля. Старые исторические logs/HISTORY и намеренные
unsupported-target cases не менять глобальной заменой строк.

### Этапы и критерии

CORE8.0 **завершён11ноября2026**: [baseline/подтверждённые failures](HISTORY.md#core80-baseline-2026-11-11). CORE8.1 завершён в managed build-input/profile scope: [профили, CPM и independent delivery](HISTORY.md#core81-profiles-2026-11-11). CORE8.2 завершён: [четыре полные suites D/R и132package DI/IPCcases](HISTORY.md#core82-generators-2026-11-11). CORE8.3 завершён: [согласованный frozen game profile, full Aether/Editor D/R и SDK11-only actual runtime proof](HISTORY.md#core83-frozen-2026-11-11). CORE8.4 завершён: [полная runtime-async регрессия D/R и ограниченный source/published native профиль](HISTORY.md#core84-runtime-2026-11-11). CORE8.5 завершён: [свежая согласованная поставка](HISTORY.md#core85-delivery-2026-11-11).

| Этап | Результат и границы | Проверка завершения |
| --- | --- | --- |
| CORE8.0 — завершён,2026-11-11 | Зафиксировать HEAD/user diff, effective SDK/TFM/Features, все46solution/61tracked projects,11test projects, источники NuGet и support profile. Получить ordinary restore/build baseline с binlogs, отделить среду/сеть/stale assets от исходных ошибок. Сначала воспроизвести текущий runtime-async=on; bounded off-control использовать для diagnosis | [Получен baseline](HISTORY.md#core80-baseline-2026-11-11): root restore0/build D/R0errors/1556warnings;4manual NU1201 и1/1failed repository-game test/SDK selector.61evaluation47net11/14net10;1767source/build hashes unchanged. Command logs/binlogs/TRX сохранены; full suites не запускались |
| CORE8.1 — завершён, 2026-11-11, managed inputs | [Общий SDK/TF/TestsTF profile](HISTORY.md#core81-profiles-2026-11-11), 45 active runners и обе templates; CPM ограничен owners, obsolete CLI tool удалён, package-only consumers изолированы | 61 net11 evaluations; обычные source builds D/R0errors, внешний SDK11 NU1008 устранён без props copy; Cobalt package consumer D/R и16packages source/published/fresh по230checks/4owned children на вид поставки. Остальные native/UI/performance scripts проверены syntax/profile, execution acceptance — CORE8.4/5; actual frozen game restore после selector — CORE8.3 |
| CORE8.2 — завершён, 2026-11-11, генераторы и Roslyn | Cobalt и Enerit: SDK compiler host против pinned Roslyn API, language version и dynamic compilations в tests, загрузка analyzer из ProjectReference и пакета. Править зависимости только при подтверждённой несовместимости; сохранить diagnostics/generated contracts | [Проверено](HISTORY.md#core82-generators-2026-11-11): Cobalt runtime179/generator96 и Enerit runtime354/generator114 каждый D/R, failed/skipped0; SDK Roslyn5.12/pinned5.9 без loader/compiler errors.8fresh packages,132DI/IPCscenarios D/R build/publish, analyzer/buildTransitive closure и negative CS0234. Исправлены только тестовые ожидания DI11 aliases и trace Recorded mask; production API/dependencies сохранены |
| CORE8.3 — завершён, 2026-11-11, Aether frozen game build | Обновить SDK selection, restore/build arguments, snapshot/lock/package policy, manifests/cache validation и fixtures на согласованный net11 профиль. Сохранить read/capture boundaries, identities, source observer/cancel/cleanup; определить обращение с прежними artifacts. Держать изменения у engine owner | [Проверено](HISTORY.md#core83-frozen-2026-11-11): full Aether3243 и Editor1496 каждый D/R с ordinary restore; module target/SDK/Features согласованы, schema1 manifest/lock net11.0. SDK11-only root: actual repository game compile/verify/reuse/Open→Runtime Step/checkpoint→Stop/reap, source Debug/published Release,2children. Старые/future manifest и SDK policies отклоняются; bounds/identity/ownership сохранены |
| CORE8.4 — завершён, 2026-11-11, runtime async и полная регрессия | Проверить on в App/lifecycle/config reload, factories/dispose, IPC/process cancellation/drain, Nom IO и Editor callbacks. Сверить новые .NET/DI behavioral changes с существующими контрактами. Выполнить все 11 test projects D/R с явной discovery; Aether process-heavy fixtures последовательно | [Проверено](HISTORY.md#core84-runtime-2026-11-11): 11 suites — по 8567 passed / 0 failed / 0 skipped D/R; discovery сопоставлена с TRX, SDK 11/net11/runtime-async=on на обычных inputs. Исправлены 4 manual template, повторены NativeRuntimeProcessTests 4/4 D/R и 24 source Debug / published Release native CPU/GPU/Editor/lifecycle сценария. Новых on failures нет, off-control не понадобился. Прежние reliability/physical/release gates сохраняются |
| CORE8.5 — завершён, 2026-11-11, поставка и документация | Одна свежая версия 16 packages; SDK/TFM/Features snapshot, независимые consumers вне repo inheritance, package-only graphs и новые caches; root/module README, AGENTS и migration guide согласованы | [Проверено](HISTORY.md#core85-delivery-2026-11-11): source/published/fresh App — 690 checks/12 children; 4 source/published host cases, 8 package game cases на обоих DI, 2 fresh native GPU profiles; runtime/analyzer/buildTransitive/native hashes и dependency versions согласованы. Новые 22 full runs: по 8567 passed/0 failed/0 skipped D/R, ordinary solution builds прошли. Wider release/physical/reliability gates сохраняются |

Порядок: CORE8.0 → CORE8.1/CORE8.2 → CORE8.3 → CORE8.4 → CORE8.5.
Если CORE8.1 build упирается в генераторы, сначала закрыть CORE8.2, затем повторить
общий build; этап8.1 не объявлять завершённым по частичной компиляции. После фиксации
общих inputs engine часть8.3 и ограниченная работа с generator consumers8.2 могут
идти в разных областях; общие props/versions и итоговый evidence имеет одного владельца.

### Итоговая готовность

Обычные команды из свежего/изолированного output восстанавливают и собирают solution
в D/R; все11test projects действительно обнаружены и прошли без unexplained skips.
Existing tests меняются только при изменившемся допустимом профиле или подтверждённой
runtime semantics; не ослаблять assertions и deadlines ради зелёного результата.
Game builds, generators, source/published/fresh package consumers и соответствующий
native smoke проверены на окончательных inputs. Не требовать удаления пользовательских
bin/obj/.codex: для контроля stale outputs использовать изолированные рабочие каталоги.
Старые net10 passing counts остаются историей. Physical input, K7/device loss/VRAM,
Nom VM/power-loss и wider release gates сохраняют прежний scope и не закрываются миграцией.

### Первичные внешние источники

Сверены11ноября2026: [SDK11.0.100/runtime11.0.0](https://dotnet.microsoft.com/en-us/download/dotnet/11.0),
[breaking changes](https://learn.microsoft.com/dotnet/core/compatibility/11),
[Runtime Async и требования runtime](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/runtime).
Документ runtime содержит RC2-oriented текст; окончательные compiler/runtime semantics
сверять с installed11.0.100 и актуальными release notes в CORE8.0/4. Ссылки не заменяют
выполнение проверок TrueMoon и не подтверждают найденные в нём дефекты.

## Критерии готовности

- Известный набор тестов действительно обнаружен и пройден, skipped/failed указаны; tests existence не считается passing результатом.
- Startup failure, cancellation и shutdown не оставляют принадлежащие app сервисы/подписки; ошибки доступны caller по согласованному контракту.
- Configuration и diagnostics работают вне UI и не требуют renderer/window.
- Согласованный bootstrap сценарий работает на заявленном DI provider; неподдержанные provider semantics описаны.
- Build/package consumer и пример воспроизводятся с указанными SDK/TFM/packages; публикация UI не заменяет этот consumer.

## После первой рабочей проверки

Расширять configuration/diagnostics по реальным приложениям; измерять startup и allocations перед оптимизацией. Новые providers и дополнительные lifecycle API вводить отдельным решением. Параметры общего выпуска — в G/R критериях [общего плана](../PLAN.md).
