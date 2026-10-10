# Enerit: состояние и точка продолжения

Дата: **2026-10-09 — чтение исходников и создание плана**. [План](PLAN.md), [общий статус](../STATUS.md). Новые build/tests/process/transport checks не запускались.

## Реализация и проверки

| Область | Наличие реализации | Свидетельство |
| --- | --- | --- |
| Invocation | Client/server/factories/handlers, service storage и app registration | [IInvocationClient](../../TrueMoon.Enerit/IO/IInvocationClient.cs), [Registration](../../TrueMoon.Enerit/AppCreationContextExtensions.cs); чтение |
| Transports | IO/Pipes и IO/MemoryMappedFiles implementations | Исходники найдены, capability/lifecycle readiness не подтверждена запуском |
| Serialization/generation | ISerializer, serialization helpers, ServicesGenerator/SignalsMappingGenerator | [ISerializer](../../TrueMoon.Enerit/IO/ISerializer.cs), [Generator](../../TrueMoon.Enerit.Generator/ServicesGenerator.cs); чтение |
| Сценарии | Enerit.Tests и Enerit.Generator.Tests, pipes/serialization/generator sources | Среди прочитанного MemoryMappedFileTests содержит закомментированные scenarios; это не passing checks. Новые discovery/results отсутствуют |

## Блокеры и ограничения

Текущий runtime/transport/generator baseline неизвестен. Не установлены проверенные limits/error/cancellation/disconnect и package consumer scope. Наличие обоих transports не даёт основания обещать одинаковую production поддержку.

## Следующий конкретный шаг

ENE0: получить отдельные discovery/results runtime и generator, записать реально активные transport scenarios. Выбрать один transport для ENE1/ENE2 и собственного client/server процесса; failures фиксировать отдельно от generated serialization checks.

## Последнее изменение документации

2026-10-09 создан PLAN/STATUS, IPC/generator выделены из общей сводки. Текущая работа подтверждает структуру исходников, не успешность протокола. HISTORY создаётся после появления фактических записей.
