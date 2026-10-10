# Cobalt / DI: план модуля

Первичный roadmap2026-10-09. [Статус](STATUS.md), [общий план](../PLAN.md), [правила](../PLANNING.md). Новые этапы запланированы, конкретные API/semantics определяются перед работой.

## Границы

TrueMoon.Cobalt, Cobalt.Generator, Cobalt.Tests, Cobalt.Generator.Tests, TrueMoon.Extensions.DependencyInjection и ManualTests/CobaltTest. Ответственность: регистрация и разрешение сервисов, lifetime/disposal, generated resolvers и provider adapter к общим service contracts.

App startup/stop/rollback принадлежит [Core](../core/PLAN.md). Cobalt отвечает за resolver ownership; совместные сценарии относятся к G2/G5. UI registration/Hosting остаётся в [UI плане](../alloy/PLAN.md).

## Цель

Пользователь регистрирует сервисы, выбирает поддержанный DI provider и получает предсказуемые resolution/lifetime/error semantics. Generated и runtime пути согласованы; generator доставляется и работает в независимом consumer.

## Архитектура и решения

Сохранять IServiceResolver/IServicesRegistrationContext/IServiceResolverBuilder в Contracts. Cobalt runtime использует Contracts и свой generator; Microsoft DI adapter использует Contracts. Общие service contracts не получают зависимости на конкретный контейнер.

Зафиксировать реально поддержанные lifetimes, factory/instance ownership, разрешение нескольких регистраций и generics. Возможности разных providers не объявлять идентичными заранее; различия описать и проверить. Новые scopes или автоматическую container migration этот roadmap не вводит.

### Принятые контракты исправления ревью — 2026-10-10

Основание: исправление Core integration и выявленных resolver/generator дефектов. [Проверки и ограничения](HISTORY.md).

- Generated factory выбирается по service type, implementation type и lifetime runtime-регистрации. Переданные factories остаются runtime-owned; лишние generated declarations не добавляют сервисов в runtime graph.
- Singleton instance принадлежит конкретному resolver, создаётся один раз при конкурентном resolution. Composite aliases2–8 разрешаются в один concrete instance; closed/open generics и typed IEnumerable используют тот же graph. Optional missing-service resolution возвращает null/default, required — наблюдаемую ошибку.
- Cobalt владеет созданными generated/factory values, включая transients, но не переданными instance registrations. Disposal учитывает reference identity, идёт в обратном порядке, продолжает cleanup после errors и сохраняет их. Sync/async mode использует соответствующий интерфейс, sync поддерживает async-only values.
- Concurrent Dispose ожидает одно завершение; рекурсивное disposal в той же async call chain не ожидает самого себя. Microsoft DI wrapper — один borrowed instance внутри provider, владеющий provider снаружи.
- Microsoft DI сохраняет стандартный учёт disposable aliases: Composite aliases имеют shared identity, но provider может вызвать Dispose одного concrete instance несколько раз. Такой сервис должен поддерживать повторный Dispose. Provider cleanup после ошибки также следует стандартному Microsoft DI поведению; гарантия reference-unique/continue-on-error относится к Cobalt.

Scopes, delivery analyzer через package и произвольная поддержка всех declarations не добавлены этим исправлением. COB4/COB5 остаются самостоятельными этапами.

## Этапы

| ID | Результат | Критерий завершения |
| --- | --- | --- |
| COB0 | Runtime/generator/adapter baseline | Отдельные discovery/results существующих test projects и CobaltTest; выбранный provider явно указан |
| COB1 | Registration/resolution contract | Concrete/interface/factory/instance, multiple registrations, generics и missing-service errors проверены в пределах поддержанного API |
| COB2 | Lifetime и disposal ownership | Повторное resolution и resolver shutdown соответствуют lifetime; owned/borrowed и sync/async disposal зафиксированы, failure cleanup проверен |
| COB3 | Согласованные runtime/generated/provider paths | Поведение сравнивается на одних service graphs; generator diagnostics и unsupported cases имеют ясный результат |
| COB4 | Analyzer/runtime package consumer | Pack и отдельная сборка потребителя подтверждают доставку analyzer/generated sources и работоспособный resolution |
| COB5 | Контрольный app и измерения | Core integration работает; startup/resolution/allocations baseline на фиксированном graph, оптимизации проверяются повтором |

COB1/COB2 согласовать с CORE1/CORE3. COB3 не означает смену DI provider по ходу app lifecycle. COB4 проверяет именно package consumption, а не только ProjectReference.

## Критерии готовности

- Поддержанные регистрация/resolution/generics/lifetime cases имеют поведенческие результаты; ошибки сохраняют полезную причину.
- Resolver освобождает свои ресурсы по контракту и не уничтожает чужие instances.
- Сгенерированные resolvers компилируются и работают; unsupported declarations получают проверяемые diagnostics.
- Provider differences документированы, bootstrap/start/stop согласован с Core.
- Независимый package consumer подтверждает generator и runtime delivery; версии/commands/configuration зафиксированы.

## После первой рабочей проверки

Расширять registration API и startup optimizations только по потребности consumers и baseline. Общая совместимость/release policy остаётся в общем плане, новые lifetime guarantees требуют отдельного решения.
