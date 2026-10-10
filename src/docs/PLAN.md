# TrueMoon: общий план развития

Создан 2026-10-09 по запросу разделить общий roadmap и планы модулей. [Текущее состояние](STATUS.md), [формат ведения планов](PLANNING.md). Это план всего фреймворка; детальные задачи и проверки находятся в документах соответствующих модулей.

## Цель

Развивать TrueMoon как модульный C# фреймворк для запуска приложений: конфигурация и lifecycle, DI, управление units/процессами, межпроцессные вызовы и необязательная UI подсистема. Пользователь должен подключать нужные функции через понятные контракты, сохранять контроль над ресурсами и получать наблюдаемые ошибки.

Проверить три сквозных сценария:

1. Приложение без UI: конфигурация, сервисы, startup, cancellation, shutdown и diagnostics.
2. Приложение с units: запуск собственного дочернего процесса, IPC вызов, завершение или отказ и согласованный restart/shutdown.
3. UI приложение и внешний HUD: Alloy/Argentis использует общие lifecycle/DI contracts; renderer/device/window ownership остаётся в UI интеграциях и host.

UI alpha и контрольное beta приложение имеют собственные критерии. Готовность UI не означает готовность всех модулей TrueMoon; обратное тоже не следует из общего build.

## Карта модулей

| Направление | Проекты и ответственность | План |
| --- | --- | --- |
| Core | TrueMoon, TrueMoon.Contracts, TrueMoon.Core: bootstrap, configuration, diagnostics, lifecycle, общие сервисные контракты и utilities | [Core](core/PLAN.md) |
| Cobalt / DI | TrueMoon.Cobalt, Cobalt.Generator, Extensions.DependencyInjection: resolver, регистрация, lifetime, generation и альтернативная DI интеграция | [Cobalt](cobalt/PLAN.md) |
| Enerit | TrueMoon.Enerit, Enerit.Generator: invocation client/server, IPC transports, serialization и generated service adapters | [Enerit](enerit/PLAN.md) |
| Mithril | TrueMoon.Mithril: units, hosting/startup/lifetime/restart policies, процессные handles | [Mithril](mithril/PLAN.md) |
| Alloy / Argentis | Argentis, Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting: retained UI, renderer, окно/input, standalone и external HUD | [UI](alloy/PLAN.md) |

Тестовые проекты, generators и ManualTests относятся к своим направлениям. Они не образуют отдельный продуктовый roadmap. Общие CI/build/package задачи остаются на уровне этого документа.

## Архитектура и зависимости

Фактические ProjectReference, сверенные2026-10-09:

- Contracts и Core не имеют ProjectReference на другие модули. Общие app/service contracts находятся в Contracts, utilities — в Core.
- TrueMoon использует Contracts, Cobalt и Cobalt.Generator. Cobalt использует Contracts и собственный generator; Extensions.DependencyInjection использует Contracts.
- Enerit использует Contracts, Core и свой generator. Mithril использует Contracts/Core; прямой ProjectReference Mithril→Enerit отсутствует. Их взаимодействие проверяется как composition приложения.
- Argentis не имеет ProjectReference; Alloy использует Argentis. Rendering.Skia и Platform.Silk используют Alloy. Alloy.Hosting связывает эти интеграции и общие Contracts/Core.

Сохранять отсутствие зависимости общего bootstrap/DI/IPC/units от UI и графических пакетов. Контракты интеграции не должны требовать конкретного renderer, DI provider или IPC transport без явного выбора. Генератор относится к своему модулю; доставка analyzer в package проверяется отдельно от работы через ProjectReference.

Изменение этой карты фиксировать здесь и в локальном плане, не выполняя перестройку зависимостей ради нового формата документации. Вопросы будущего API и версионирования протоколов решаются в соответствующих этапах.

## Порядок развития

| Этап | Результат | Зависимость и готовность |
| --- | --- | --- |
| G1. Исходное состояние | Сверенная карта проектов, scope модулей, перечень поддерживаемых конфигураций и отдельные baseline результаты build/tests | Документы созданы; проверки всех модулей ещё нужны. Каждый failure закреплён за модулем, skipped и пустая discovery не считаются passing coverage |
| G2. Bootstrap и DI | Проверенные startup/stop/cancellation/error/disposal contracts приложения и обоих выбранных DI путей | [CORE1–CORE3](core/PLAN.md), [COB1–COB3](cobalt/PLAN.md). Ошибка интеграции разбирается на app/resolver границе |
| G3. IPC и units | Рабочий ограниченный сценарий собственного child process с invocation и управляемым завершением/restart | [ENE1–ENE3](enerit/PLAN.md), [MIT1–MIT3](mithril/PLAN.md), затем совместный integration proof |
| G4. UI alpha | Закрытые UI критерии K1–K9 и consumer/native delivery на заявленной конфигурации | [UI маршрут](alloy/PLAN.md#актуальная-точка-продолжения--реализация-dpi-2026-10-09). Работать независимо от G3; общие Core/DI ошибки остаются integration блокерами там, где влияют на UI |
| G5. Сквозные приложения | Небольшие воспроизводимые examples для headless, units/IPC и UI; failures/cancellation/cleanup подтверждены вне отдельных component tests | G2 и нужные локальные этапы. UI не обязателен для headless/units сценария, IPC не обязателен для UI |
| G6. Поставка и общий выпуск | Package consumers, CI/configuration matrix, compatibility notes и воспроизводимые release artifacts | Проверенные критерии выпуска ниже; version suffix и solution build не заменяют их |

G1–G6 — общий roadmap, а не новые UI требования. За исключением выполненной документальной карты в G1, готовность этих этапов требует отдельных запусков. Даты выпуска и новые публичные API этим планом не назначаются.

Актуализация2026-10-10: solution baseline получен, известные Core/Cobalt defects исправлены и имеют behavioral tests; Mithril discovery0 явно выделена. Ближайший общий шаг — CORE5/COB4 независимый headless package consumer, параллельно локальным ENE1/ENE2 и MIT0 own-process сценариям. Продолжение UI остаётся owner dispatcher и физической DPI/monitor matrix по собственному плану. Ограниченное UI окружение не останавливает независимые общие работы.

## Сквозные правила

- **Lifecycle и ownership:** определить владельца app, service, process, transport и graphics resources; startup failure, cancellation и shutdown имеют проверяемый результат и cleanup.
- **Ошибки и диагностика:** исходная причина доступна вызывающему коду; module error не маскируется успехом другого сценария. Согласовать, какие diagnostics нужны для расследования отказа.
- **Конфигурация:** проверять обязательные параметры до запуска внешних ресурсов; precedence/providers и изменяемость settings документировать в Core.
- **Generators:** сохранять runtime/generated contracts, проверять диагностику ошибок и поставку analyzer в отдельном consumer.
- **Производительность:** измерять контрольный сценарий перед оптимизацией. UI K9 остаётся локальным baseline; его результат не переносится на IPC/DI/process startup.
- **Совместимость:** breaking changes и migration notes фиксировать у владельца API; объединённый release описывает их вместе. Сейчас общая версия0.2.2.7-alpha задаётся [Directory.Build.props](../Directory.Build.props), отдельные версии модулей не вводятся этой реорганизацией.

## Общие критерии выпуска

| Критерий | Требуемое свидетельство |
| --- | --- |
| R1. Сборка и проверки | Датированные restore/build и результаты по каждому заявленному тестовому проекту, включая discovery, failed/skipped и ограничения. Политика warnings и поддерживаемые SDK/TFM записаны явно |
| R2. Контракты | Локальные критерии включённых в выпуск модулей пройдены; непроверенные режимы не выдаются за поддержанные |
| R3. Интеграция | Собственные процессы, DI, cancellation и ошибки проверены в заявленных сквозных examples; ресурсы не остаются после завершения |
| R4. Поставка | Pack/publish и независимые consumers подтверждают references, generators, transitive dependencies, native/assets delivery; GPU consumer проверяется на заявленной UI конфигурации |
| R5. Документация | Карта подключения модулей, рабочие примеры, ownership/threading/errors, ограничения и migration notes соответствуют выпускаемому артефакту |
| R6. Воспроизводимость | Версии source/SDK/packages/native/assets/configuration и точные команды сохранены; release readiness не выводится из исторических результатов |

Набор модулей общего выпуска и заявленные платформы определяются явно перед G6. Локальный выпуск одного модуля проверяется по его собственным критериям и зависимостям; он не помечает общий roadmap завершённым.

## После первой проверенной поставки

Развивать реальные consumers: расширение конфигурации и диагностики Core, DI после измерения startup/allocations, новые IPC capabilities по потребности приложения, операционную диагностику units и UI beta по PA1–PA10. Эти направления уточняются после первой рабочей проверки соответствующего модуля; сетевые transports, distributed orchestration или новые UI API не обещаются текущим общим планом.

## Поддержание плана

Соблюдать [общий формат](PLANNING.md). Новые глобальные задачи получают G/R идентификаторы, локальные — идентификаторы своего модуля. Подробные проверки и история хранятся локально; общий STATUS содержит ссылки и межмодульные ограничения. Согласованные UI1–5/K1–K9/PA1–PA10 сохраняются.
