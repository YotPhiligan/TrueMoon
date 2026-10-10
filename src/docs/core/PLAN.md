# Core: план bootstrap и общих контрактов

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Этапы ниже запланированы; конкретные semantics уточняются по существующему API перед реализацией.

## Границы

Входят TrueMoon, TrueMoon.Contracts, TrueMoon.Core и TrueMoon.Tests. Bootstrap/App.Builder, configuration, diagnostics и app lifecycle находятся в TrueMoon; независимые публичные contracts — в Contracts, utilities/buffers/scheduler — в Core. Это группа планирования, не предложение объединить сборки.

DI resolver и generators относятся к [Cobalt](../cobalt/PLAN.md), IPC — к [Enerit](../enerit/PLAN.md), units/processes — к [Mithril](../mithril/PLAN.md), UI — к [Alloy/Argentis](../alloy/PLAN.md). Core определяет их общие app/service boundaries и не получает графические зависимости.

## Цель

Приложение без UI предсказуемо строится, читает конфигурацию, запускает сервисы, сообщает diagnostics, завершается по cancellation и освобождает принадлежащие ему ресурсы. Неудачный startup имеет наблюдаемую причину и проверяемый cleanup.

## Архитектура и решения

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

## Критерии готовности

- Известный набор тестов действительно обнаружен и пройден, skipped/failed указаны; tests existence не считается passing результатом.
- Startup failure, cancellation и shutdown не оставляют принадлежащие app сервисы/подписки; ошибки доступны caller по согласованному контракту.
- Configuration и diagnostics работают вне UI и не требуют renderer/window.
- Согласованный bootstrap сценарий работает на заявленном DI provider; неподдержанные provider semantics описаны.
- Build/package consumer и пример воспроизводятся с указанными SDK/TFM/packages; публикация UI не заменяет этот consumer.

## После первой рабочей проверки

Расширять configuration/diagnostics по реальным приложениям; измерять startup и allocations перед оптимизацией. Новые providers и дополнительные lifecycle API вводить отдельным решением. Параметры общего выпуска — в G/R критериях [общего плана](../PLAN.md).
