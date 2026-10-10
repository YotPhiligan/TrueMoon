# Cobalt / DI: состояние и точка продолжения

Дата: **2026-10-09 — чтение исходников и создание плана**. [План](PLAN.md), [общий статус](../STATUS.md). Новые build/runtime/generator/adapter checks не запускались.

## Реализация и проверки

| Область | Наличие реализации | Свидетельство |
| --- | --- | --- |
| Resolution/registration | Resolver, containers, factory/instance/generic abstractions и registration contexts | [CobaltServiceResolverBase](../../TrueMoon.Cobalt/CobaltServiceResolverBase.cs), [ServiceLifetime](../../TrueMoon.Cobalt/ServiceLifetime.cs); чтение |
| Disposal | DisposablesContainer и sync/async resolver disposal | Исходники runtime; успешный lifecycle проход не подтверждён |
| Generator | CobaltGenerator и resolver source models | [Generator](../../TrueMoon.Cobalt.Generator/CobaltGenerator.cs), Cobalt.Generator.Tests; наличие, не run |
| Microsoft DI adapter | ServiceResolver/Builder/RegistrationContext | [Adapter](../../TrueMoon.Extensions.DependencyInjection/ServiceResolver.cs); только чтение |
| Сценарии | Cobalt.Tests, Cobalt.Generator.Tests, ManualTests/CobaltTest | Новая test discovery и passing counts этой работой не получены |

## Блокеры и ограничения

Не получен текущий baseline provider/lifetime/generation/consumer. Исторический AppCreate failure — общий вопрос на границе Core/DI, исходная причина не устанавливалась этой сверкой. Не считать его доказательством конкретного дефекта Cobalt или Microsoft DI adapter.

## Следующий конкретный шаг

COB0: отдельно проверить runtime и generator test projects, затем одинаковый небольшой registration/lifecycle graph на Cobalt и adapter. Зафиксировать differences/failures и выбрать COB1/COB2 вместе с CORE1.

## Последнее изменение документации

2026-10-09 создан PLAN/STATUS, generator и DI adapter включены в одно направление. Проверки документации не являются новыми passing runtime/generator results. HISTORY появится при накоплении датированных запусков.
