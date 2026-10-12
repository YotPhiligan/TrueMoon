# Документация TrueMoon

TrueMoon состоит из bootstrap/lifecycle, DI, IPC, units/processes и необязательного UI. Входная карта проекта и quickstart находятся в [корневом README](../../README.md). Эта страница помогает выбрать документ по задаче; текущая готовность определяется датированными проверками в STATUS, а не наличием проекта или API.

## Общие документы

| Документ | Для чего читать |
| --- | --- |
| [Общий план](PLAN.md) | Цели, границы модулей, зависимости, сквозные сценарии и критерии выпуска |
| [Общий статус](STATUS.md) | Текущая сводка, подтверждённые проверки, блокеры и ближайшие действия |
| [Формат планирования](PLANNING.md) | Правила ведения PLAN/STATUS/HISTORY и разделения общей и модульной работы |

## Модули

| Направление | Проекты и ответственность | Документы |
| --- | --- | --- |
| Core | TrueMoon, TrueMoon.Contracts, TrueMoon.Core: App/configuration/diagnostics, lifecycle, общие contracts и utilities | [План и контракты](core/PLAN.md), [статус](core/STATUS.md), [история](core/HISTORY.md) |
| Cobalt / DI | TrueMoon.Cobalt, Cobalt.Generator, Extensions.DependencyInjection: registration/resolution, lifetime, generation, Microsoft DI adapter | [План](cobalt/PLAN.md), [статус](cobalt/STATUS.md), [история](cobalt/HISTORY.md) |
| Enerit | TrueMoon.Enerit и Enerit.Generator: invocation, serialization и IPC transports | [План](enerit/PLAN.md), [статус](enerit/STATUS.md), [история](enerit/HISTORY.md) |
| Mithril | TrueMoon.Mithril: units, process handles и startup/hosting/lifetime/restart policies | [План](mithril/PLAN.md), [статус](mithril/STATUS.md), [история](mithril/HISTORY.md) |
| Alloy / Argentis | Argentis, Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting: controls/tree/bindings/layout/input, UI sessions, rendering, окна и HUD | [План](alloy/PLAN.md), [статус](alloy/STATUS.md), [история](alloy/HISTORY.md) |

Contracts и Core — разные независимые сборки. DI provider выбирается на builder boundary; IPC, units и UI подключаются по потребности приложения. UI runtime не требует App или конкретного backend; Hosting является опциональной интеграцией. Подробная карта фактических зависимостей хранится в общем плане.

## Практические входные точки

- **Приложение и сервисы:** quickstart в корневом README, затем [Core lifecycle/configuration/diagnostics contracts](core/PLAN.md#принятые-контракты-исправления-ревью--2026-10-10) и [DI ownership](cobalt/PLAN.md#принятые-контракты-исправления-ревью--2026-10-10).
- **Headless NuGet и generator:** [CORE5/COB4 package consumers](../ManualTests/CorePackageConsumer/README.md), [App package README](../TrueMoon/PACKAGE.md), [Cobalt](../TrueMoon.Cobalt/PACKAGE.md), [generator](../TrueMoon.Cobalt.Generator/PACKAGE.md) и [Microsoft DI adapter](../TrueMoon.Extensions.DependencyInjection/PACKAGE.md).
- **UI и привязки:** [Hosting guide](../TrueMoon.Alloy.Hosting/README.md), [Settings example](../ManualTests/AlloyTest/README.md) и [packaged quickstart](../TrueMoon.Alloy.Hosting/PACKAGE.md).
- **Renderer/native/GPU ownership:** [Rendering.Skia](../TrueMoon.Alloy.Rendering.Skia/README.md), [Vulkan interop](alloy/VULKAN-INTEROP.md), [manual probes](../ManualTests/AlloyVulkanTest/README.md) и [performance baseline](alloy/PERFORMANCE_BASELINE.md).
- **NuGet и native assets:** [independent consumer](../ManualTests/AlloyPackageConsumer/README.md). Его проверенная конфигурация — Windows x64/net10.0/framework-dependent build/publish без RID; это не общий release proof.
- **Фиксированные шрифты и UI screenshots:** [K2/K4 golden guide](alloy/GOLDEN_TESTS.md): font snapshots,30эталонов, pixel tolerance, Debug/Release runner и диагностические diff PNG.
- **Owned child lifecycle:** [bounded Mithril probe](../ManualTests/MithrilLifecycleProbe/README.md): Never handle startup/shutdown/disposal и Debug/published Release matrix.
- **App/units и parent lifecycle:** [Mithril composition probe](../ManualTests/MithrilCompositionProbe/README.md): main/child/owned external, rollback и parent exit на Cobalt/DI, App/Never scope.
- **Bounded restart:** [Mithril restart probe](../ManualTests/MithrilRestartProbe/README.md): policies, attempts/delay, cancelled scheduling и terminal lifetime,40Debug/published Release cases.
- **Bounded own-process pipes:** [Enerit probe](../ManualTests/EneritPipesProbe/README.md): generated payloads,32concurrent, local cancellation, errors/disconnect/reconnect и cleanup.
- **IPC и units composition:** [MithrilTest](../ManualTests/MithrilTest/Program.cs), затем локальные статусы Enerit/Mithril. App+IPC/restart proof ещё требуется; пример не является доказательством готовности.
- **Старый UI API:** [Compatibility guide](../TrueMoon.Alloy.Hosting/Compatibility/README.md); наличие legacy реализации не означает исправление её старых defects.

## Как читать готовность

PLAN хранит согласованные цели/контракты и критерии. STATUS отделяет наличие реализации от успешно выполненных проверок на конкретной конфигурации. HISTORY сохраняет команды, failures, эксперименты и ограничения предыдущих запусков. Сведения других модулей и прошлых запусков не переносятся в текущий passing результат.

Core/DI package consumer и текущие Core81/Cobalt37/generator6 регрессии проверены
2026-10-11; UI package/golden того же дня имеют отдельный scope и не повторены
headless работой. Mithril MIT0 проверен отдельным run2026-10-11:14/14 Debug,
14/14 Release, manual16/16 в Never child-handle scope. Enerit отдельный run2026-10-11: runtime71/71 и generator34/34 Debug/Release, manual12/12 same-build pipes.
Последующий MIT1/MIT2 run: Mithril65/65 Debug/Release, App matrix28/28, fresh legacy16/16; Enerit runtime regression71/71 в каждой configuration. Следующий MIT3 проверен2026-10-12: Mithril89/89 Debug/Release, restart40/40, fresh App Never28/28 и legacy16/16. Теперь общий шаг — MIT4/ENE4 App+IPC+restart.
Внешний NVIDIA K7, physical DPI,
real loss/VRAM и общий own-process IPC/units proof остаются открыты. Актуальный
порядок работ смотрите в общем и локальных STATUS.
