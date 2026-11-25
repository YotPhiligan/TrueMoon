# Документация TrueMoon

Актуальная сверка зависимостей и native DLL: [DEP1 —2026-11-15](core/PLAN.md#dep1-dependency-refresh), [результаты/SHA](core/DEPENDENCY_REFRESH_20261115.json).

TrueMoon состоит из bootstrap/lifecycle, DI, IPC, units/processes и необязательного UI. Входная карта проекта и quickstart находятся в [корневом README](../../README.md). Эта страница помогает выбрать документ по задаче; текущая готовность определяется датированными проверками в STATUS, а не наличием проекта или API.

## Kul / Audio

[TrueMoon.Kul: сборка, локальный native feed и App/DI](audio/README.md),
[API/ownership](../TrueMoon.Kul/PACKAGE.md), [план](audio/PLAN.md),
[текущие проверки](audio/STATUS.md). Windows x64 AUD1;
[optional Aether ECS/captured-content bridge](audio/ECS_BRIDGE.md) — ARCH1h.2a.

## Kton / Physics

[TrueMoon.Kton и BEPU provider: App/configuration/ownership](physics/APP.md),
[контракт](physics/CONTRACTS.md), [план](physics/PLAN.md),
[текущие проверки](physics/STATUS.md). PHYS0–6 завершены в проверенных профилях,
включая [Aether ECS bridge](physics/ECS_BRIDGE.md) и контроллер персонажа.
[PHYS8](physics/PLAN.md#phys8) содержит приёмку визуального consumer и дальнейшие
профили нагрузки.

## Слои Aether и поставка

[Выбор слоя, Mod SDK, migration и ownership](engine/ARCHITECTURE_GUIDE.md),
[итоговая карта переносов](engine/ARCH39_MIGRATION.csv),
[сквозной runner](../ManualTests/ArchitectureDelivery/README.md).

## Mod reload и migration

[Stable World/reload SDK, callbacks и ownership](engine/MOD_RELOAD.md);
[проверки stable profile](engine/ARCH37_BASELINE.json).
[Private components и migration нового World](engine/MOD_MIGRATION.md),
[migration baseline](engine/ARCH38_BASELINE.json); [сквозная ARCH3.9 приёмка/SHA](engine/ARCH39_BASELINE.json).

## Общие документы

| Документ | Для чего читать |
| --- | --- |
| [Общий план](PLAN.md) | Цели, границы модулей, зависимости, сквозные сценарии и критерии выпуска |
| [.NET 11: профиль и переход](core/DOTNET11_MIGRATION.md) | SDK/TFM/runtime-async, independent consumers и пересборка game artifacts |
| [Общий статус](STATUS.md) | Текущая сводка, подтверждённые проверки, блокеры и ближайшие действия |
| [Формат планирования](PLANNING.md) | Правила ведения PLAN/STATUS/HISTORY и разделения общей и модульной работы |
| [Единая конфигурация](core/CONFIGURATION_GUIDE.md) | Рабочее руководство: sources/приоритеты/typed options/App/reload/запись и совместимость |
| [139 стандартных ключей](core/CONFIGURATION_KEYS.md) | Каталог17schemas, types/defaults/aliases из public metadata свежих пакетов |
| [App/child configuration delivery](../ManualTests/ConfigurationDelivery/README.md) | Единая независимая source/published/fresh-package проверка на обоих DI |
| [Аудит размещения типов и namespace — 2026-10-21](HISTORY.md#аудит-размещения-типов-и-namespace--2026-10-21) | Все44проекта, namespace замечания, технические группы типов и порядок исправления |
| [Исправления структуры — 2026-10-21](HISTORY.md#исправление-структуры-проектов--2026-10-21) | Выполненные переносы, миграция public API, проверки и исправление PID lookup в GPU примере |
| [Проверка package metadata](../eng/Check-PackageMetadata.ps1) | MSBuild properties; `-IncludeProject <paths>` ограничивает scope, `-Pack -NoBuild` проверяет готовые Release nupkg/nuspec и README/license/icon/analyzer/native payloads |

## Модули

| Направление | Проекты и ответственность | Документы |
| --- | --- | --- |
| Core | TrueMoon, TrueMoon.Contracts, TrueMoon.Core: App/configuration/diagnostics, lifecycle, общие contracts и utilities | [План и контракты](core/PLAN.md), [статус](core/STATUS.md), [история](core/HISTORY.md) |
| Cobalt / DI | TrueMoon.Cobalt, Cobalt.Generator, Extensions.DependencyInjection: registration/resolution, lifetime, generation, Microsoft DI adapter | [План](cobalt/PLAN.md), [статус](cobalt/STATUS.md), [история](cobalt/HISTORY.md) |
| Enerit | TrueMoon.Enerit и Enerit.Generator: invocation, serialization и IPC transports | [План](enerit/PLAN.md), [статус](enerit/STATUS.md), [история](enerit/HISTORY.md) |
| Mithril | TrueMoon.Mithril: units, process handles и startup/hosting/lifetime/restart policies | [План](mithril/PLAN.md), [статус](mithril/STATUS.md), [история](mithril/HISTORY.md) |
| Nom / asset storage | TrueMoon.Nom: transactional KV/CAS, App integration, snapshots/GC/backup и byte/raw unmanaged API; NOM0–NOM8 проверены в описанном local scope; public namespaces организованы по подсистемам | [README](../TrueMoon.Nom/README.md), [структура/API migration](nom/STRUCTURE.md), [план](nom/PLAN.md), [статус](nom/STATUS.md), [решение](nom/STORAGE_DESIGN.md) |
| Kton / 3D physics | PHYS4 App/configuration/factory/lifecycle:192checks/profile обоих DI; current provider309×3; далее ECS bridge | [App guide](physics/APP.md), [queries/events](physics/QUERIES_CONTACTS.md), [provider](physics/PROVIDER.md), [контракт](physics/CONTRACTS.md), [план](physics/PLAN.md), [статус](physics/STATUS.md), [результаты](physics/HISTORY.md#phys4-2026-11-12), [runner](../ManualTests/KtonApp/README.md) |
| Alloy / Argentis | Argentis, Alloy, Rendering.Skia, Platform.Silk, Alloy.Hosting: controls/tree/bindings/layout/input, UI sessions, rendering, окна и HUD | [План](alloy/PLAN.md), [статус](alloy/STATUS.md), [стандартный стиль](alloy/DEFAULT_STYLE.md), [история](alloy/HISTORY.md) |
| Aether / Engine | Abstractions + самостоятельный Runtime; headless Tooling compiler/authoring/project workflow; отдельные consumers | [README](../TrueMoon.Aether/README.md), [план](engine/PLAN.md), [статус](engine/STATUS.md), [история](engine/HISTORY.md) |
| Aether Editor | Editor.UI поверх Runtime; optional Editor.Tooling для project/document/build/authoring workflow | [Слои/API/миграция](engine/EDITOR_LAYERS.md), [full host](../TrueMoon.Aether.Editor.Tooling/README.md), [ED1–ED8](engine/EDITOR_PLAN.md) |

Contracts и Core — разные независимые сборки. DI provider выбирается на builder boundary; IPC, units и UI подключаются по потребности приложения. UI runtime не требует App или конкретного backend; Hosting является опциональной интеграцией. Подробная карта фактических зависимостей хранится в общем плане.

## Игровой модуль Aether и дальнейшее развитие

[Abstractions](../TrueMoon.Aether.Abstractions/PACKAGE.md), [самостоятельный Runtime](engine/RUNTIME.md)
и [headless Tooling](engine/TOOLING.md) разделены в ARCH3.1–4; [Editor.UI/optional bridge](engine/EDITOR_LAYERS.md) — в ARCH3.5.
Существующий Editor/Game.Runtime пример и игровая DLL остаются отдельными consumers,
тесты находятся в TrueMoon.Aether.Tests. Следующий этап после bounded quality прохода определён в
[сводной матрице качества](QUALITY.md); это оценка scopes и evidence, не новый roadmap.

- [Игровой движок](engine/PLAN.md), [статус](engine/STATUS.md): одна 3D-игра,
  Editor/Runtime в разных процессах, frames и обновление игровой логики; датированные
  реализации и ограничения находятся в STATUS/HISTORY.
- [Подключаемый редактор ED1–ED8](engine/EDITOR_PLAN.md): после
  [ARCH2 package/host перехода](engine/PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27)
  существующая ENG7a/UIA1 основа развивается в project/document editor, preview, undo и расширения.
  Первый срез — Open → Transform → preview → Undo/Redo → Save → reopen.
  [Контракт камеры/selection ED5a.0](engine/EDITOR_CAMERA_SELECTION.md) и
  [численный probe](../ManualTests/AetherCameraContractProbe/README.md) фиксируют следующий viewport profile.

## Практические входные точки

- **Диагностика:** [Core pipeline, JSONL и операции](core/DIAGNOSTICS.md), [Enerit traces/metrics](enerit/DIAGNOSTICS.md) и [bounded child → host delivery](engine/DIAGNOSTICS_DELIVERY.md).
- **Engine/Editor packages:** [карта типов, scene-persistence/runtime API и ownership ARCH2a–c](engine/PACKAGE_BOUNDARIES.md), [Editor App API](../TrueMoon.Aether.Editor/PACKAGE.md), [отдельный runtime host](../Examples/AetherRuntimeHost/README.md); hosts/resources/package acceptance продолжается в ARCH2d/e.
- **Приложение и сервисы:** quickstart в корневом README, затем [Core lifecycle/configuration/diagnostics contracts](core/PLAN.md#принятые-контракты-исправления-ревью--2026-10-10) и [DI ownership](cobalt/PLAN.md#принятые-контракты-исправления-ревью--2026-10-10).
- **Headless NuGet и generator:** [CORE5/COB4 package consumers](../ManualTests/CorePackageConsumer/README.md), [App package README](../TrueMoon/PACKAGE.md), [Cobalt](../TrueMoon.Cobalt/PACKAGE.md), [generator](../TrueMoon.Cobalt.Generator/PACKAGE.md) и [Microsoft DI adapter](../TrueMoon.Extensions.DependencyInjection/PACKAGE.md).
- **UI и привязки:** [Hosting guide](../TrueMoon.Alloy.Hosting/README.md), [Settings example](../ManualTests/AlloyTest/README.md) и [packaged quickstart](../TrueMoon.Alloy.Hosting/PACKAGE.md).
- **RichTextEditor:** [API/options/fonts/binding](alloy/RICH_TEXT_EDITOR.md), [запускаемый пример с toolbar](../ManualTests/RichTextEditorTest/README.md) и [отложенный план улучшений](alloy/RICH_TEXT_EDITOR_PLAN.md).
- **Engine sessions:** [ENG1 headless probe](../ManualTests/EngineSessionProbe/README.md): fresh owned child generations, Hello/scene readiness,20Start/Stop и bounded crash/cancel/cleanup; ENG2 CPU viewport/owned pixel image и запуск окна описаны в том же guide.
- **Renderer/native/GPU ownership:** [Rendering.Skia](../TrueMoon.Alloy.Rendering.Skia/README.md), [Vulkan interop](alloy/VULKAN-INTEROP.md), [manual probes](../ManualTests/AlloyVulkanTest/README.md) и [performance baseline](alloy/PERFORMANCE_BASELINE.md).
- **NuGet и native assets:** [independent consumer](../ManualTests/AlloyPackageConsumer/README.md). Его проверенная конфигурация — Windows x64/net10.0/framework-dependent build/publish без RID; это не общий release proof.
- **Стратегия UI-проверок без Computer Use:** [UiSession, raster, тестовый host и native evidence](alloy/UI_TESTING.md).
- **Фиксированные шрифты и UI screenshots:** [K2/K4 golden guide](alloy/GOLDEN_TESTS.md): font snapshots,30эталонов, pixel tolerance, Debug/Release runner и диагностические diff PNG.
- **Owned child lifecycle:** [bounded Mithril probe](../ManualTests/MithrilLifecycleProbe/README.md): Never handle startup/shutdown/disposal и Debug/published Release matrix.
- **App/units и parent lifecycle:** [Mithril composition probe](../ManualTests/MithrilCompositionProbe/README.md): main/child/owned external, rollback и parent exit на Cobalt/DI, App/Never scope.
- **Bounded restart:** [Mithril restart probe](../ManualTests/MithrilRestartProbe/README.md): policies, attempts/delay, cancelled scheduling и terminal lifetime,40Debug/published Release cases.
- **Bounded own-process pipes:** [Enerit probe](../ManualTests/EneritPipesProbe/README.md): generated payloads,32concurrent, local cancellation, errors/disconnect/reconnect и cleanup.
- **Independent IPC/units packages:** [UnitsPackageConsumer](../ManualTests/UnitsPackageConsumer/README.md):108cases Cobalt/DI Debug/Release build/publish, actual analyzer provenance, DI без Cobalt analyzer и omitted Enerit analyzer negative control.
- **App/generated IPC/restart:** [AppUnitsPipesProbe](../ManualTests/AppUnitsPipesProbe/README.md):36Debug/published Release cases на Cobalt/DI, pending crash без replay, limit exhaustion, rollback и child cleanup при parent exit. Старый [MithrilTest](../ManualTests/MithrilTest/Program.cs) остаётся примером.
- **Миграция UI:** [Переход с Hosting.Compatibility на явный Hosting API](../TrueMoon.Alloy.Hosting/README.md#миграция-с-hostingcompatibility).

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

- **Минимальный Player ENG7b.1–2:** [API/CLI/ownership](engine/PLAYER.md),
  [Player-only](../Examples/AetherPlayer/README.md), [combined host](../Examples/AetherHost/README.md),
  [native proof](../ManualTests/PlayerProbe/README.md); expanded matrix остаётся ENG7b.3.
- **Aether Abstractions ARCH3.1:** [контрактный SDK и explicit ABI migration](engine/ABSTRACTIONS.md),
  [current map](engine/ARCH31_TYPE_MAP.csv), [independent extension/host proof](../ManualTests/Abstractions.Host/README.md).
  Actual World/scheduler binding — следующий ARCH3.2; старые artifacts не retag автоматически.
