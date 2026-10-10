# Core: план bootstrap и общих контрактов

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Этапы ниже запланированы; конкретные semantics уточняются по существующему API перед реализацией.

## Границы

Входят TrueMoon, TrueMoon.Contracts, TrueMoon.Core и TrueMoon.Tests. Bootstrap/App.Builder, configuration, diagnostics и app lifecycle находятся в TrueMoon; независимые публичные contracts — в Contracts, utilities/buffers/scheduler — в Core. Это группа планирования, не предложение объединить сборки.

DI resolver и generators относятся к [Cobalt](../cobalt/PLAN.md), IPC — к [Enerit](../enerit/PLAN.md), units/processes — к [Mithril](../mithril/PLAN.md), UI — к [Alloy/Argentis](../alloy/PLAN.md). Core определяет их общие app/service boundaries и не получает графические зависимости.

## Цель

Приложение без UI предсказуемо строится, читает конфигурацию, запускает сервисы, сообщает diagnostics, завершается по cancellation и освобождает принадлежащие ему ресурсы. Неудачный startup имеет наблюдаемую причину и проверяемый cleanup.

## Архитектура и решения

Сохранять App.Builder и IServiceResolver/IStartable/IStoppable/IAppLifetime boundaries, отделяя bootstrap от реализации DI. Текущий TrueMoon ссылается на Cobalt/его generator и Contracts; это зависимость существующей реализации, не изменение новым планом.

Перед CORE1 определить правила повторного Start/Stop, cancellation, порядка запуска/остановки и rollback. Перед CORE2 определить configuration precedence/validation и lifecycle diagnostics subscriptions. Не объявлять эти semantics уже гарантированными только по именам API.

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
