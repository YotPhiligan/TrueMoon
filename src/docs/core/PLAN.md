# Core: план bootstrap и общих контрактов

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Этапы ниже запланированы; конкретные semantics уточняются по существующему API перед реализацией.

## Границы

Входят TrueMoon, TrueMoon.Contracts, TrueMoon.Core и TrueMoon.Tests. Bootstrap/App.Builder, configuration, diagnostics и app lifecycle находятся в TrueMoon; независимые публичные contracts — в Contracts, utilities/buffers/scheduler — в Core. Это группа планирования, не предложение объединить сборки.

DI resolver и generators относятся к [Cobalt](../cobalt/PLAN.md), IPC — к [Enerit](../enerit/PLAN.md), units/processes — к [Mithril](../mithril/PLAN.md), UI — к [Alloy/Argentis](../alloy/PLAN.md). Core определяет их общие app/service boundaries и не получает графические зависимости.

## Цель

Приложение без UI предсказуемо строится, читает конфигурацию, запускает сервисы, сообщает diagnostics, завершается по cancellation и освобождает принадлежащие ему ресурсы. Неудачный startup имеет наблюдаемую причину и проверяемый cleanup.

## Архитектура и решения

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

## Критерии готовности

- Известный набор тестов действительно обнаружен и пройден, skipped/failed указаны; tests existence не считается passing результатом.
- Startup failure, cancellation и shutdown не оставляют принадлежащие app сервисы/подписки; ошибки доступны caller по согласованному контракту.
- Configuration и diagnostics работают вне UI и не требуют renderer/window.
- Согласованный bootstrap сценарий работает на заявленном DI provider; неподдержанные provider semantics описаны.
- Build/package consumer и пример воспроизводятся с указанными SDK/TFM/packages; публикация UI не заменяет этот consumer.

## После первой рабочей проверки

Расширять configuration/diagnostics по реальным приложениям; измерять startup и allocations перед оптимизацией. Новые providers и дополнительные lifecycle API вводить отдельным решением. Параметры общего выпуска — в G/R критериях [общего плана](../PLAN.md).
