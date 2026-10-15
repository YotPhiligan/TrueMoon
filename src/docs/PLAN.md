# TrueMoon: общий план развития

Создан 2026-10-09 по запросу разделить общий roadmap и планы модулей. [Корневой README](../../README.md), [индекс документации](README.md), [текущее состояние](STATUS.md), [формат ведения планов](PLANNING.md). Это план всего фреймворка; детальные задачи и проверки находятся в документах соответствующих модулей.

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

Актуальный порядок, сверка2026-10-11: CORE5/COB4 independent headless package/analyzer
consumer проверен на Windows x64/net10.0/SDK10.0.401 — 64 matrix + 3 delivered README
processes, Debug/Release build/publish без RID; текущие Core81/Cobalt37/generator6
регрессии прошли. [Детальный сценарий у Core](core/PLAN.md#core5--cob4-независимый-headless-consumer--2026-10-11).
MIT0 bounded own-child baseline проверен2026-10-11: Mithril14/14 Debug и14/14
Release, manual16/16 Debug/published Release; `Never` handle scope без App/IPC.
[Локальный контракт](mithril/PLAN.md#mit0-own-child-контракт--2026-10-11).
ENE1/ENE2 bounded same-build pipes проверен2026-10-11: runtime71/71 и generator34/34
в Debug/Release, manual12/12; [контракт](enerit/PLAN.md#ene1-ene2-контракт--2026-10-11).
MIT1/MIT2 App/controller/parent-exit проверен2026-10-11: Mithril65/65 Debug/Release, App matrix28/28 на Cobalt/DI, fresh legacy16/16 в App/Never scope; [контракт](mithril/PLAN.md#mit1mit2-app-composition-контракт--2026-10-11).
MIT3 bounded restart проверен2026-10-12: Mithril89/89 Debug/Release, restart40/40, App Never regression28/28 и legacy16/16; [контракт](mithril/PLAN.md#mit3-bounded-restart-контракт--2026-10-12).
MIT4/ENE4 bounded composition проверен2026-10-14: Enerit102/102, generator34/34,
Mithril89/89, Core81/81, Cobalt37/37/generator6/6 отдельно Debug/Release;
App/pipes/restart matrix36/36 Cobalt/DI Debug/published Release, isolated pipes12/12.
[Единый контракт](mithril/PLAN.md#mit4ene4-app-и-generated-pipes-контракт--2026-10-14).
Enerit strict response/decode ownership проверены далее2026-10-14:
runtime126/126, generator114/114, Mithril89/89 в Debug/Release, fresh App36/isolated12;
[локальный контракт](enerit/PLAN.md#ene1ene2-strict-response-и-decode-ownership--2026-10-14).
MIT5/ENE5 package portion проверена далее2026-10-14:9nupkgs,108consumer cases,
Cobalt/DI включая DI без Cobalt analyzer, Debug/Release build/publish;
[контракт](mithril/PLAN.md#mit5ene5-independent-package-contract--2026-10-14).
На момент package portion ближайшим шагом был измеренный IPC baseline до оптимизаций;
На тот момент operational diagnostics MIT5 и performance ENE5 были открытыми частями;
их отдельные результаты2026-10-15 сведены в [quality matrix](QUALITY.md).
Текущий приоритет уточнён ниже2026-10-14: качество существующих модулей, начиная с Cobalt.
UI owner/package5.4–5.5 и fixed-font K2/K4 проверены ранее2026-10-11:
UI532/532, golden360/360, validation0/0; эти UI проверки текущим headless шагом не повторены.
NVIDIA GDI defect остаётся внешним K7 блокером; поиск app-side workaround отложен
по решению пользователя, lifecycle acceptance и условия возврата сохраняются.
Physical DPI/4.9, real loss/VRAM и итоговый alpha этап остаются в UI плане.

## Будущее игровое направление — 2026-10-13

По обсуждению зафиксирован [план engine/editor](engine/PLAN.md) для одной
не очень сложной 3D-игры: Editor и Game.Runtime в разных процессах, viewport
кадры, обновление игровой логики, сохранение editor state и standalone game.
[Статус](engine/STATUS.md): ENG0–ENG8 запланированы, проектов/реализации и
собственных passing checks ещё нет. Детальные contracts/tasks ведутся там.

Направление использует Core/Cobalt, Mithril/Enerit и Alloy как отдельные
boundaries, не меняет текущую dependency map. Repeatable unit sessions, frame
transport и cross-process GPU ownership требуют новой реализации и проверки.
MIT4/ENE4 завершён в bounded scope2026-10-14; текущий функциональный приоритет —
качество существующих модулей, начиная с Cobalt, после проверенной MIT5/ENE5 package portion. Выбор первого
game scenario/platform/API и начало ENG0 относятся к будущему запуску работы.
UI alpha/K1–K9/PA1–PA10 и внешний K7 блокер не отменяются.

## Качество модулей до engine/editor — 2026-10-14

**Текущая сводка2026-10-15:** [quality matrix](QUALITY.md) собрала bounded результаты
Core/Cobalt/Enerit/Mithril и открытый UI acceptance. Следующий code этап —
[UI package/Hosting quality](alloy/PLAN.md#проход-качества-ui-после-headless-модулей--2026-10-15)
после bundled Cobalt/explicit fonts; K7/physical DPI/loss/VRAM сохраняются.
Это документационная сверка без нового build/tests/native pass. Ниже сохранена
датированная последовательность решений, а не список всё ещё открытых шагов.

По решению пользователя сначала довести текущие модули до рабочего уровня качества,
затем начинать движок. Первое направление — [Cobalt COB6/COB5/COB7](cobalt/PLAN.md#качество-cobalt-до-engineeditor--решение2026-10-14):
корректность и диагностика, удобство существующего API, измерения, сравнение с
Microsoft DI и условный переход consumers. Автоматическая миграция не назначена.

Уточнение2026-10-14: выбран собственный TrueMoon API и **два Cobalt projects в
одном consumer package**. Microsoft DI ecosystem bridge/контракты не являются
целью; существующий adapter сохраняется. [Принятый scope/поставка](cobalt/PLAN.md#принятый-scope-и-один-пакет--2026-10-14).

Уточнение2026-10-15 после [COB7 comparison](cobalt/COMPARISON.md): Cobalt COB6a–d,
COB5 baseline и COB7 проверены в bounded headless scope. Cobalt остаётся default,
массовая migration explicit Microsoft DI consumers отложена; adapter сохраняется.
Далее собственный Core quality review по порядку ниже. Подтверждённый hot-path
backlog Cobalt ведётся в его плане отдельно, UI migration требует своего proof.

Уточнение2026-10-15 после [Core quality](core/PLAN.md#сквозной-headless-proof--2026-10-15):
configuration/bootstrap, diagnostics и цельный headless source/package proof
проверены в bounded Windows x64/net10.0 scope. Ограничения Core сохранены в его
STATUS; новых APIs/runtime fixes финальный proof не потребовал. Следующий модуль —
Enerit quality review и ENE5 measured IPC baseline, затем Mithril operational
diagnostics. Это не общий выпуск и не завершение readiness остальных модулей.

Уточнение2026-10-15 после [ENE5 baseline](enerit/BASELINE.md):10payload/concurrency
cases и current Enerit runtime/generator regression проверены; измерительная часть
ENE5 закрыта в same-build pipes scope. Старый package proof сохраняет свою дату,
MMF/versioning/backpressure и другие режимы не объявляются готовыми. Следующий
функциональный этап — Mithril MIT5 operational diagnostics. Enerit allocation/CPU
profile, как и Cobalt hot-path investigation, остаётся отдельным measured backlog
перед выбором оптимизаций, без автоматического расширения API.

Рабочий уровень оценивается в заявленном scope каждого модуля:

- Поддержанные API/limitations и ownership contracts понятны из примера и документации.
- Invalid input, startup/operation failure и cancellation имеют ясную исходную
  причину; повторные операции и cleanup соответствуют контракту.
- Есть текущие поведенческие проверки модуля и нужной интеграции, включая errors
  и lifecycle; успешный соседний модуль не подменяет собственные результаты.
- Independent consumer подтверждает нужные runtime/analyzer/native assets;
  warnings рассмотрены по причине, а не массово подавлены.
- Performance workload и baseline зафиксированы перед оптимизацией; неподтверждённые
  платформы, транспорты и deployment modes остаются явно ограниченными.

Детальные задачи остаются у владельцев: после Cobalt — Core bootstrap/configuration/
diagnostics, Enerit serialization/transport/errors и ENE5 measurements, Mithril
lifecycle/operational diagnostics, Alloy/Argentis UI acceptance. Объём следующего
прохода определяется ревью соответствующего модуля. ENE3/MMF и новые DI/UI APIs
не становятся обязательными только ради расширения возможностей.
UI K1–K9/PA1–PA10 и внешний K7 сохраняются; подготовка аппаратного окружения
для physical DPI/real loss/VRAM остаётся отдельным условием проверки.
Перед ENG0 свести достигнутый уровень и оставшиеся ограничения этих модулей;
требование качества не означает закрытие всех будущих roadmap пунктов.

Уточнение2026-10-15: [Mithril MIT5](mithril/PLAN.md#mit5-operational-diagnostics--2026-10-15)
дополнил bounded quality проход process diagnostics и свежей independent package
regression. Следующий этап — сводная quality matrix Core/Cobalt/Enerit/Mithril и
оставшегося UI acceptance перед выбором ENG0. Other platform modes, долгие stress
checks и measured allocation backlog не становятся автоматически новым functional scope.
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
