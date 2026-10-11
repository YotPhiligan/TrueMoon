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
| Mithril | TrueMoon.Mithril: units, process handles и startup/hosting/lifetime/restart policies | [План](mithril/PLAN.md), [статус](mithril/STATUS.md) |
| Alloy / Argentis | Argentis, Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting: controls/tree/bindings/layout/input, UI sessions, rendering, окна и HUD | [План](alloy/PLAN.md), [статус](alloy/STATUS.md), [история](alloy/HISTORY.md) |

Contracts и Core — разные независимые сборки. DI provider выбирается на builder boundary; IPC, units и UI подключаются по потребности приложения. UI runtime не требует App или конкретного backend; Hosting является опциональной интеграцией. Подробная карта фактических зависимостей хранится в общем плане.

## Практические входные точки

- **Приложение и сервисы:** quickstart в корневом README, затем [Core lifecycle/configuration/diagnostics contracts](core/PLAN.md#принятые-контракты-исправления-ревью--2026-10-10) и [DI ownership](cobalt/PLAN.md#принятые-контракты-исправления-ревью--2026-10-10).
- **UI и привязки:** [Hosting guide](../TrueMoon.Alloy.Hosting/README.md), [Settings example](../ManualTests/AlloyTest/README.md) и [packaged quickstart](../TrueMoon.Alloy.Hosting/PACKAGE.md).
- **Renderer/native/GPU ownership:** [Rendering.Skia](../TrueMoon.Alloy.Rendering.Skia/README.md), [Vulkan interop](alloy/VULKAN-INTEROP.md), [manual probes](../ManualTests/AlloyVulkanTest/README.md) и [performance baseline](alloy/PERFORMANCE_BASELINE.md).
- **NuGet и native assets:** [independent consumer](../ManualTests/AlloyPackageConsumer/README.md). Его проверенная конфигурация — Windows x64/net10.0/framework-dependent build/publish без RID; это не общий release proof.
- **IPC и units:** [MithrilTest composition](../ManualTests/MithrilTest/Program.cs), затем локальные статусы Enerit/Mithril. Успешный own-process lifecycle run ещё требуется; пример не является доказательством готовности.
- **Старый UI API:** [Compatibility guide](../TrueMoon.Alloy.Hosting/Compatibility/README.md); наличие legacy реализации не означает исправление её старых defects.

## Как читать готовность

PLAN хранит согласованные цели/контракты и критерии. STATUS отделяет наличие реализации от успешно выполненных проверок на конкретной конфигурации. HISTORY сохраняет команды, failures, эксперименты и ограничения предыдущих запусков. Сведения других модулей и прошлых запусков не переносятся в текущий passing результат.

Последние записи о Core/DI/Enerit component tests относятся к 2026-10-10, UI package consumer — к 2026-10-11. Mithril discovery0 не является passing suite. Внешний NVIDIA K7 blocker, fixed-font/full golden, physical DPI, real loss и VRAM остаются в UI статусе; Core/Cobalt independent headless/analyzer delivery и Enerit/Mithril own-process evidence имеют собственные задачи. Актуальный порядок работ смотрите в общем и локальных STATUS.
