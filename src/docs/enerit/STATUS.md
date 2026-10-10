# Enerit: состояние и точка продолжения

Дата: **2026-10-10 — объединение inherited generated mapping fix и повторные проверки в main**. [План](PLAN.md), [история проверок](HISTORY.md), [общий статус](../STATUS.md). Агентный коммит `6e554e3` объединён с Alloy/Argentis; состояние исходников при проверках — `d56f217`.

В main повторены runtime **10passed/0failed/0skipped** и generator **9passed/0failed/0skipped**, Windows/Debug/net10.0/SDK10.0.401. Команды и TRX — [интеграционная запись](HISTORY.md#интеграция-в-main--2026-10-10); результаты исходного агентного baseline отдельно ниже.

## Реализация и проверки

| Область | Наличие реализации | Свидетельство текущего запуска |
| --- | --- | --- |
| Runtime / ENE0 | Invocation, factories/handlers, serializer и оба transports | Windows, Debug/net10.0, SDK10.0.401, VSTest/xUnit2.9.3: discovery10; Passed10, Failed0, Skipped0 до и после изменения generator |
| Generator / ENE0 | ServicesGenerator/SignalsMappingGenerator и serialization helpers | Исходная discovery4; Passed4, Failed0, Skipped0. После добавления regression: Passed9, Failed0, Skipped0 |
| Pipes | Raw named-pipe exchange, один invocation и два concurrent invocation в одном testhost | Активные `PipesTests.Test0/Test1/Test2` прошли. Это in-process Windows evidence; `Test0` не содержит assertions, `Test1/Test2` проверяют только non-null результаты |
| Memory-mapped | Runtime implementation присутствует | `MemoryMappedFileTests.Test1/Test2` полностью закомментированы; discovery0, не skipped/passing. Transport readiness не подтверждена |
| Generated mapping / ENE1 | Client и handler используют единый список declared/inherited методов; одинаковые inherited signatures объединяются, handler вызывает declaring interface | Пять новых real-consumer compilation/emit cases: direct, inherited, diamond, independent duplicate-signatures и distinct overloads. Проверены одинаковые IDs client/handler и сохранение declared IDs |

Исходные4 generator checks не заменяют новые compilation checks: прежний snapshot вызывает `UseSignalService`, а `ServicesGenerator` ищет `UseInvocationService`/`ListenInvocationService`; старый helper не подключает metadata references и не проверяет output compilation/assertions. Его passing результат означает только выполнение имеющихся тестов.

## Изменения

Исправлен локальный bug `ServicesGenerator`: `GetMembers()` исключал методы базовых интерфейсов, вызывая CS0535 в generated client. Новый список сохраняет declared order, добавляет inherited методы, объединяет эквивалентные сигнатуры независимо от имён параметров. Dispatch через declaring interface устраняет CS0121 для одинаковых inherited signatures; diamond не дублирует метод, разные overloads сохраняются. Новые тесты действительно запускают generator, проверяют diagnostics и emit обоих adapters против metadata references runtime/Contracts.

Публичные runtime/Contracts API и общие build/package настройки не менялись. При интеграции обновлён общий STATUS. ENE0 подтверждён для указанной Windows Debug конфигурации; ENE1 выполнен частично, весь serialization contract не закрыт.

## Блокеры и ограничения

- Own-process invocation, cancellation/disconnect/error/cleanup, malformed/unsupported payloads и независимый package consumer ещё не проверены этим запуском.
- Для inherited mapping добавлены новые codes после declared методов; общий порядок inherited methods остаётся связан с Roslyn symbols и одинаковым интерфейсом на обеих сторонах. Межверсионная совместимость protocol mapping не установлена.
- Предел byte method code не исправлялся: client использует byte counter, handler — int. Более256 методов и соответствующая generator diagnostic требуют отдельного шага.
- Разные return types для одинаковых signatures, generic/static members и дополнительные типы payload не входят в новые пять cases.
- Существующие build warnings (включая duplicate central PackageVersion/несколько sources/nullability) остаются; full solution и Enerit Release checks не запускались. Отдельные UI проверки интеграции не подтверждают Enerit transport readiness.

## Следующий конкретный шаг

Продолжить ENE1: проверить реальную сериализацию/null/collections и malformed payloads отдельно от compilation. Для ENE2 первым кандидатом остаются pipes; требуется bounded own-process scenario с cancellation/error/disconnect и cleanup. MMF остаётся отдельно неподтверждённым.
