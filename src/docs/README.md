# Документация TrueMoon

TrueMoon состоит из bootstrap/lifecycle, DI, IPC, units/processes и необязательного UI. Входная карта проекта и quickstart находятся в [корневом README](../../README.md). Эта страница помогает выбрать документ по задаче; текущая готовность определяется датированными проверками в STATUS, а не наличием проекта или API.

## Общие документы

| Документ | Для чего читать |
| --- | --- |
| [Общий план](PLAN.md) | Цели, границы модулей, зависимости, сквозные сценарии и критерии выпуска |
| [Общий статус](STATUS.md) | Текущая сводка, подтверждённые проверки, блокеры и ближайшие действия |
| [Формат планирования](PLANNING.md) | Правила ведения PLAN/STATUS/HISTORY и разделения общей и модульной работы |
| [Аудит размещения типов и namespace — 2026-10-21](HISTORY.md#аудит-размещения-типов-и-namespace--2026-10-21) | Все44проекта, namespace замечания, технические группы типов и порядок исправления |
| [Исправления структуры — 2026-10-21](HISTORY.md#исправление-структуры-проектов--2026-10-21) | Выполненные переносы, миграция public API, проверки и исправление PID lookup в GPU примере |
| [Проверка package metadata](../eng/Check-PackageMetadata.ps1) | Вычисленные MSBuild properties всех проектов; с `-Pack` — локальные nupkg/nuspec и README/license/icon/analyzer/native payloads |

## Модули

| Направление | Проекты и ответственность | Документы |
| --- | --- | --- |
| Core | TrueMoon, TrueMoon.Contracts, TrueMoon.Core: App/configuration/diagnostics, lifecycle, общие contracts и utilities | [План и контракты](core/PLAN.md), [статус](core/STATUS.md), [история](core/HISTORY.md) |
| Cobalt / DI | TrueMoon.Cobalt, Cobalt.Generator, Extensions.DependencyInjection: registration/resolution, lifetime, generation, Microsoft DI adapter | [План](cobalt/PLAN.md), [статус](cobalt/STATUS.md), [история](cobalt/HISTORY.md) |
| Enerit | TrueMoon.Enerit и Enerit.Generator: invocation, serialization и IPC transports | [План](enerit/PLAN.md), [статус](enerit/STATUS.md), [история](enerit/HISTORY.md) |
| Mithril | TrueMoon.Mithril: units, process handles и startup/hosting/lifetime/restart policies | [План](mithril/PLAN.md), [статус](mithril/STATUS.md), [история](mithril/HISTORY.md) |
| Alloy / Argentis | Argentis, Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting: controls/tree/bindings/layout/input, UI sessions, rendering, окна и HUD | [План](alloy/PLAN.md), [статус](alloy/STATUS.md), [история](alloy/HISTORY.md) |
| Aether / Engine | TrueMoon.Aether: engine contracts, sessions/control, owned child bridge и CPU frame transport; отдельные example/game/test consumers | [README](../TrueMoon.Aether/README.md), [план](engine/PLAN.md), [статус](engine/STATUS.md), [история](engine/HISTORY.md) |

Contracts и Core — разные независимые сборки. DI provider выбирается на builder boundary; IPC, units и UI подключаются по потребности приложения. UI runtime не требует App или конкретного backend; Hosting является опциональной интеграцией. Подробная карта фактических зависимостей хранится в общем плане.

## Игровой модуль Aether и дальнейшее развитие

Engine implementation/contracts теперь объединены в [TrueMoon.Aether](../TrueMoon.Aether/README.md).
Существующий Editor/Game.Runtime пример и игровая DLL остаются отдельными consumers,
тесты находятся в TrueMoon.Aether.Tests. Следующий этап после bounded quality прохода определён в
[сводной матрице качества](QUALITY.md); это оценка scopes и evidence, не новый roadmap.

- [Игровой движок и редактор](engine/PLAN.md), [статус](engine/STATUS.md):
  одна 3D-игра, Editor/Game.Runtime в разных процессах, preview frames и
  обновление игровой логики. ENG0 определён, ENG1 headless supervisor реализован
  и проверен в ManualTests; ENG2 CPU viewport реализован, replacement и ENG3–ENG8 ещё запланированы.

## Практические входные точки

- **Приложение и сервисы:** quickstart в корневом README, затем [Core lifecycle/configuration/diagnostics contracts](core/PLAN.md#принятые-контракты-исправления-ревью--2026-10-10) и [DI ownership](cobalt/PLAN.md#принятые-контракты-исправления-ревью--2026-10-10).
- **Headless NuGet и generator:** [CORE5/COB4 package consumers](../ManualTests/CorePackageConsumer/README.md), [App package README](../TrueMoon/PACKAGE.md), [Cobalt](../TrueMoon.Cobalt/PACKAGE.md), [generator](../TrueMoon.Cobalt.Generator/PACKAGE.md) и [Microsoft DI adapter](../TrueMoon.Extensions.DependencyInjection/PACKAGE.md).
- **UI и привязки:** [Hosting guide](../TrueMoon.Alloy.Hosting/README.md), [Settings example](../ManualTests/AlloyTest/README.md) и [packaged quickstart](../TrueMoon.Alloy.Hosting/PACKAGE.md).
- **RichTextEditor:** [API/options/fonts/binding](alloy/RICH_TEXT_EDITOR.md), [запускаемый пример с toolbar](../ManualTests/RichTextEditorTest/README.md) и [отложенный план улучшений](alloy/RICH_TEXT_EDITOR_PLAN.md).
- **Engine sessions:** [ENG1 headless probe](../ManualTests/EngineSessionProbe/README.md): fresh owned child generations, Hello/scene readiness,20Start/Stop и bounded crash/cancel/cleanup; ENG2 CPU viewport/owned pixel image и запуск окна описаны в том же guide.
- **Renderer/native/GPU ownership:** [Rendering.Skia](../TrueMoon.Alloy.Rendering.Skia/README.md), [Vulkan interop](alloy/VULKAN-INTEROP.md), [manual probes](../ManualTests/AlloyVulkanTest/README.md) и [performance baseline](alloy/PERFORMANCE_BASELINE.md).
- **NuGet и native assets:** [independent consumer](../ManualTests/AlloyPackageConsumer/README.md). Его проверенная конфигурация — Windows x64/net10.0/framework-dependent build/publish без RID; это не общий release proof.
- **Фиксированные шрифты и UI screenshots:** [K2/K4 golden guide](alloy/GOLDEN_TESTS.md): font snapshots,30эталонов, pixel tolerance, Debug/Release runner и диагностические diff PNG.
- **Owned child lifecycle:** [bounded Mithril probe](../ManualTests/MithrilLifecycleProbe/README.md): Never handle startup/shutdown/disposal и Debug/published Release matrix.
- **App/units и parent lifecycle:** [Mithril composition probe](../ManualTests/MithrilCompositionProbe/README.md): main/child/owned external, rollback и parent exit на Cobalt/DI, App/Never scope.
- **Bounded restart:** [Mithril restart probe](../ManualTests/MithrilRestartProbe/README.md): policies, attempts/delay, cancelled scheduling и terminal lifetime,40Debug/published Release cases.
- **Bounded own-process pipes:** [Enerit probe](../ManualTests/EneritPipesProbe/README.md): generated payloads,32concurrent, local cancellation, errors/disconnect/reconnect и cleanup.
- **Independent IPC/units packages:** [UnitsPackageConsumer](../ManualTests/UnitsPackageConsumer/README.md):108cases Cobalt/DI Debug/Release build/publish, actual analyzer provenance, DI без Cobalt analyzer и omitted Enerit analyzer negative control.
- **App/generated IPC/restart:** [AppUnitsPipesProbe](../ManualTests/AppUnitsPipesProbe/README.md):36Debug/published Release cases на Cobalt/DI, pending crash без replay, limit exhaustion, rollback и child cleanup при parent exit. Старый [MithrilTest](../ManualTests/MithrilTest/Program.cs) остаётся примером.
- **Старый UI API:** [Compatibility guide](../TrueMoon.Alloy.Hosting/Compatibility/README.md); наличие legacy реализации не означает исправление её старых defects.

## Как читать готовность

PLAN хранит согласованные цели/контракты и критерии. STATUS отделяет наличие реализации от успешно выполненных проверок на конкретной конфигурации. HISTORY сохраняет команды, failures, эксперименты и ограничения предыдущих запусков. Сведения других модулей и прошлых запусков не переносятся в текущий passing результат.

Сводный [рабочий уровень модулей перед движком](QUALITY.md) содержит scopes,
датированные evidence и оставшиеся ограничения. Core/Cobalt/Enerit/Mithril имеют
завершённый bounded quality проход; следующий этап — UI package/Hosting proof
после текущих Cobalt/fonts изменений. UI alpha/K7/physical DPI/real loss/VRAM
и общий release остаются открыты. Engine/editor — запланированное направление.
Сверка2026-10-15 документационная: прежние build/tests/package/native результаты
не считаются новым запуском. Подробная история сохраняется у владельцев и в
[общей HISTORY](HISTORY.md), актуальная очередность — в [общем STATUS](STATUS.md).
