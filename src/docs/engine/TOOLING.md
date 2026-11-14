# Headless Tooling — ARCH3.4

Ревизия 2026-11-14. Отдельные assembly/package/RootNamespace:
`TrueMoon.Aether.Tooling`. [Очередь](PLAN.md#current-engine-queue), [статус](STATUS.md),
[карта](ARCH34_MIGRATION.csv), [manifest/SHA](ARCH34_BASELINE.json).

| Область | Текущий owner |
| --- | --- |
| Нейтральные game/ECS/scene/render values | Abstractions |
| World/session/prepared loading/control/frame/backend/resources | Runtime |
| Compiler/preflight/capture/publisher, authoring/history/persistence, project/document/source sessions | Tooling |
| GameEditor/ScenePreview и full AetherHost workflow, headless preview scheduling | Tooling поверх Runtime |
| Diagnostic writer и pure input DTO | Tooling.Diagnostics |
| Views/presentation ports/data/styles и Runtime-only adapter | Editor.UI, без Tooling; [ARCH3.5](EDITOR_LAYERS.md) |
| Concrete project/document/inspector/diagnostics VM, commands/window/full adapters | Optional Editor.Tooling поверх Tooling + UI |

Tooling не ссылается на Editor, Alloy или Argentis. Старый TrueMoon.Aether проект
убран из текущего solution/project/package wiring; imports переходят в
TrueMoon.Aether.Tooling.*. Type-forwarding facade нет. Нужен rebuild согласованной
поставки hosts/games/artifacts/captures; общая версия пакетов не менялась.
Schema2 exact identities/SHA сохранены. Tooling DLL запрещена как private game
dependency, как и старая монолитная DLL. Scene/project/control/frame/checkpoint
byte formats, IDs, exact schemas и custom component values сохраняются.

## App, DI и configuration

UseAetherTooling(options) регистрирует один headless graph с явными HostContractId
и BuildDirectory. Registration/empty startup не читает проект, не вызывает SDK,
compiler/source worker, не создаёт RuntimeEngine/окно/GPU. App единолично владеет
ToolingHost и EditorProjects; host владеет compiler product, coordinator — active/
retired project/document/source products. DI factories и overrides сохраняются;
disposable aliases и per-frame resolution не вводятся.

UseConfiguredAetherTooling(new ToolingComposition(id, runtimeAccess), configure)
использует точный ToolingConfigurationSchemas.Settings в общем App profile.
Новые headless keys Aether:Tooling:*: BuildDirectory/BuildTimeout,
BuildRetention:MaxJobs/MaxTotalBytes/MaxAge, Authoring:MaxHistoryEntries/MaxBatchEdits/
MaxOperations, SourceTracking:Enabled/UseFileSystemNotifications/Debounce/
MaximumDebounce/PollInterval/ScanBudget/MaximumWatchers. Startup settings копируются
и проверяются. HostContractId и RuntimeAccess отсутствуют в schema, поступают только
из composition root. Прежние full Aether/Editor keys и args/env/JSON priorities
сохранены. Документы/команды/изменяемое состояние не стали providers.

## Проект, Build и ownership

host.Projects.CreateAsync/OpenAsync проверяет metadata/SDK до публикации opening.
ProjectSession.Documents использует прежние authoring transactions/history и
optimistic store. Unknown game component values сохраняются через explicit schema;
неизвестные JSON envelope fields не принимаются автоматически как extensions.

host.BuildAsync(revision) захватывает opening и запускает существующий frozen
compiler с tracked inputs/compilation/preparation/project graph. Candidate выбирается
и source baseline привязывается только при актуальном открытом opening. Default
game ABI — Abstractions+Contracts; concrete Runtime — явный opt-in. Full Editor
сохраняет свою World-aware CreateBuildRequest policy. Ошибка/отмена не заменяет
LastGoodBuild и не меняет document/history. Новый opening не видит прежний result
как свой last-good. Borrowed host.Builds — low-level compiler: direct BuildAsync
не выбирает host last-good и не привязывает source baseline автоматически.

Normal Close/Switch требует Save/Discard/Cancel. Terminal Stop/Dispose закрывает
Build admission, отменяет/дожидается compiler job, затем явно discards несохранённую
память и закрывает project products; файлы не сохраняются. Cleanup failure удерживает
владельцев для retry. Source workers/hints фактически drain-ятся. File commit receipt
и cancellation остаются разными границами; conflict не перезаписывает внешний файл.

## Full workflow и следующий этап

UseAether/AetherHost теперь в Tooling и явно соединяют Runtime с compiler/GameEditor/
ScenePreview; они взаимоисключающие с UseAetherTooling в одном App. Editor переведён
на Tooling, но full adapters и project references ещё в нём. Runtime+UI без Tooling —
ARCH3.5. Internal friendship Tooling к Runtime/Abstractions явно сохраняет helpers и
ownership; обратных AssemblyReference нет.

TransformEditor сейчас Argentis control и остаётся в UI; фактические authoring
transactions находятся в Tooling.Scenes.Authoring. Diagnostic writer принимает pure
Tooling DTO, UI presentation entry/VM остаются в Editor. Report text/redaction/history/
size/cancellation policy сохранена. Existing frozen source/asset/package preparation
перенесена; новые production importers/audio tools не добавлялись.

## Приёмка и границы

[ToolingConsumer](../../ManualTests/ToolingConsumer/README.md) проверяет real project/
SDK, edit/Undo/Redo/save/reopen, frozen Build/last-good/source baseline, failed/
cancelled build/save, IDs/custom data, pure report export и cleanup на двух DI в
source/publish/fresh package-only profiles. UI assemblies и graphics/audio native
modules отсутствуют. Unit tests отдельно проверяют idle startup, mutually exclusive
composition и retained cleanup owner/retry.

[RuntimeConsumer](../../ManualTests/RuntimeConsumer/README.md) отдельно проверяет
SDK-free execution. Player/App-child/package consumers — в фактическом scope из
STATUS/HISTORY. ARCH3.6 добавляет явный [ModArtifactPublisher](MODS.md): immutable
managed bytes/manifest/hash directories без запуска модов. Collectible loader и
private Worlds принадлежат Runtime, который не получает Tooling dependency.
Other OS/RID, AOT/trimming, device loss/performance/release и mod hot replacement/
migration остаются следующими задачами.
