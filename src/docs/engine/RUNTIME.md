# Самостоятельный Runtime — ARCH3.3

Ревизия 2026-11-14. [Headless Tooling выделен в ARCH3.4](TOOLING.md). `TrueMoon.Aether.Runtime` — отдельные assembly, PackageId и
RootNamespace. Runtime исполняет подготовленную игру и сцену; исходный проект,
компилятор, authoring history, document/project workflow и Editor UI ему не нужны.
[Очередь](PLAN.md#current-engine-queue), [проверки](STATUS.md),
[manifest](ARCH33_BASELINE.json).

| Слой | Текущее размещение |
| --- | --- |
| Нейтральные game/ECS contracts, IDs, scene/input/checkpoint/render/preview values и чистые codecs | `TrueMoon.Aether.Abstractions`; только BCL + Contracts |
| World, scheduler, scene resolution/activation, sessions/control/frames, process/GPU resources и готовые backend paths | `TrueMoon.Aether.Runtime` |
| Compiler/capture/preflight/publisher, authoring/persistence, GameEditor и preview coordination | `TrueMoon.Aether.Tooling`; headless registration и project/document/source coordination |
| Project/document/source-watch coordination | Tooling; [Views/Data/styles/ports — Editor.UI; optional full adapters — Editor.Tooling](EDITOR_LAYERS.md) |
| Local Player window/input/View | `TrueMoon.Aether.Player` → Runtime + Alloy; без workflow/Editor |

Прежний `TrueMoon.Aether` проект выведен из текущей поставки.
`TrueMoon.Aether.Tooling.UseAether` явно соединяет Runtime и full workflow. Версия пакетов
по-прежнему общая; все участники поставки должны быть собраны согласованно.

## App, конфигурация и ownership

Необязательные подсистемы игровой сессии подключаются через
[`UseAetherSceneSubsystems`](SESSION_SUBSYSTEMS.md): DI-фабрики, подготовка private
scene, общий scheduler, actual Play/Pause и retained cleanup до освобождения World.
Общий слой не зависит от Kton/Kul и не включает их backend автоматически.

```csharp
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Hosting;

context.UseAetherRuntime(options =>
{
    options.HostContractId = exactHostOrPairId;
    options.GameBuildId = selectedGameId;
    options.ReportDirectory = absoluteReportDirectory;
    options.RuntimeLaunch = explicitChildCommand; // для process-host профиля
});
```

После регистрации `RuntimeEngine` предоставляет Supervisor, Frames, Replacement
и lazy Gpu. Регистрация и idle App не запускают игру, compiler или GPU.
`RuntimeOptions` не содержит BuildDirectory/BuildTimeout/BuildRetention.
`UseConfiguredAetherRuntime(RuntimeComposition, configure)` использует точный
`RuntimeConfigurationSchemas.Engine` в общем App profile. Префикс пользовательских
ключей сохранён: `Aether`; отдельные schemas World/Scheduler/SceneLoading/Primitives/
RenderResources также находятся в Runtime. Старые свойства этих runtime schemas
на `AetherConfigurationSchemas` ссылаются на те же instances. Workflow Engine и
Runtime Engine — разные schemas: выбирайте schema своего composition root.
Args/env/JSON, priorities и reload не переопределены; это startup snapshots.

Child сначала захватывает `RuntimeBootstrap`, затем регистрирует runtime core и
выбранный game/preview role. `UseConfiguredAetherRuntime(bootstrap)` делает это
вместе, сохраняя protected identity/report/delivery/parent/startup capture. В child
не нужно подключать `UseAether`. Local Player использует отдельную взаимоисключающую
точку `UseAetherLocalRuntime(PlayerStart)` и не создаёт process-host graph.

Сервисы и фабрики регистрируются через App, зависимости создаются через DI и
`IFactory<T>`/`IFactory<T,TArguments>`. World/scheduler/SceneWorld/game loader и
другие результаты фабрик остаются caller-owned; alias в DI не становится вторым
владельцем. Scheduler освобождается перед его World, game/ALC — после использующих
его render/scene resources. Ошибка cleanup сохраняет владельца для явного retry.

App — единственный disposer singleton RuntimeEngine. Workflow AetherHost заимствует
его, владеет своими compiler/preview products и запрашивает Stop core после drain
workflow. Runtime lifecycle регистрируется раньше workflow; App останавливает их
в обратном порядке. Stop core идемпотентен для успешного/in-flight cleanup и допускает
retry неуспешного. Failed creation закрывает только приобретённые ресурсы; compiler,
до которого создание не дошло, не освобождается как будто он был приобретён.

`IProcessDiagnosticsSource` в standalone Runtime сообщает `BuildAvailable=false`.
Снимки compiler diagnostics — чистые данные общей process presentation; сам producer
и source/SARIF parsing остаются в workflow. Runtime не активирует compiler ради отчёта.

## Модели, ECS и подготовленная сцена

Абстрактные и прежние дубли моделей объединены в канонических определениях
Abstractions. Например, IGameSession и IGameSessionEntry теперь находятся в
`Abstractions.Sessions` и `Abstractions.Runtime`; SceneDocument — в
`Abstractions.Scenes`; стандартные value components — в `Abstractions.Ecs.*`.
Runtime World/scheduler используют эти значения непосредственно. Constructor
handle/token проверяет форму, World — liveness/generation/ownership при использовании;
default по-прежнему невалиден. Вторых component pools или мира нет.

Для host-specific систем сохранены Runtime.Ecs.IEcsSystem/context/description с
World access. Game/mod SDK использует neutral ECS contracts; [правила binding](ECS_BINDING.md)
сохраняются. В одном source-файле при использовании обоих API задавайте явные aliases.
Старые классы из каталога `TrueMoon.Aether/Runtime/` теперь находятся в корне нового
Runtime-проекта: namespace остаётся `TrueMoon.Aether.Runtime`, без `Runtime.Runtime`.

`Runtime.Scenes.SceneDocumentCodec` владеет прежней строгой JSON grammar v1/v2 и
проверяет immutable document через runtime resolution. `SceneWorldCodec` сохраняет
TMWL v1 / length / SHA256 envelope и прежние limits. Registry/loader импортируют
готовые данные в World без source project или authoring session.
Workflow `Scenes.SceneFileCodec` добавляет authoring admission поверх той же grammar.
Runtime не ищет authoring factory или SceneFileCodec через DI; если host специально
хочет такой validator, он явно регистрирует его как SceneDocumentCodec.
Scene/project/checkpoint/control/frame byte formats этим переносом не изменены.

Для создания immutable resolved snapshots/общих helpers Abstractions временно
разрешает internal access Runtime и Tooling workflow; это не обратная
AssemblyReference и не расширение публичного mod SDK. Friendship Tooling остаётся явным для internal helpers; обратной AssemblyReference нет.

## Артефакт игры schema 2 и компиляция

`Runtime.Builds.GameBuildArtifact` выполняет только Open/validate/Pin и предоставляет
immutable manifest data. `Builds.GameArtifactPublisher` в workflow публикует результат
подготовки. `GameBuildCompiler.BuildAsync` и GameEditor возвращают `GameBuildResult`:
Artifact плюс process-local Inputs/Compilation. Повторный Open не восстанавливает
trusted source provenance; tracked/captured start требует исходного результата.

Manifest schema 2 фиксирует TargetFramework, exact HostContractId/pair identity,
SHA256 Runtime, entry/revision/private DLL set и отсортированные shared descriptors
с name/full AssemblyName/SHA256. Базовый game ABI — только Abstractions + Contracts.
`GameBuildRequest.RuntimeAccess=true` или publisher `runtimeAccess:true` явно добавляет
конкретную Runtime assembly для игр, использующих World/resource host APIs. Loader
разделяет только этот точный набор в default context; private copies Abstractions,
Contracts, Runtime и workflow Aether запрещены. Native/private dependencies всё ещё
ограничены прежним managed artifact profile.

Schema 1 отклоняется с rebuild diagnostic. Retag manifest, ослабление allowlist или
загрузка старой монолитной Aether вместо нового ABI не выполняются. Пересоберите
host, runtime и game; источник истины — новые exact binaries, не имя каталога.

Обычная и frozen compilation передают `AetherAbstractionsPath` и
`TrueMoonContractsPath`; `AetherRuntimePath` появляется только при RuntimeAccess.
Game project использует non-copying Reference. Source-tree fallback ProjectReference
на Abstractions допускается при пустом AetherAbstractionsPath; Runtime fallback —
при пустом AetherRuntimePath для явно разрешённого runtime-aware профиля.
Frozen/capture сохраняет exact shared bytes; source/compiler witnesses остаются в
workflow, без ссылки Runtime на них. Прежний `AetherContractPath` удалён из текущего
compiler profile. SDK/TFM/LangVersion/Features берутся из общего build profile.

Существующий Editor project workflow явно задаёт RuntimeAccess при создании build
request, сохраняя поддержку World/resource-aware проектов. Это политика данного
workflow; standalone request/publisher по умолчанию нейтрален. Формат project
manifest и его scene data этим не изменены; выбор политики Tooling уточняется в ARCH3.4.

## Приёмка и границы

[`RuntimeConsumer/Verify.ps1`](../../ManualTests/RuntimeConsumer/Verify.ps1) собирает
нейтральную игру отдельно, публикует artifact и сцену отдельным tooling executable,
затем запускает Runtime-only consumer на двух DI providers. Source, published и
fresh package-only профили дополняются отдельным runtime root без `sdk`, с пустым
`--list-sdks`, ограниченным PATH/DOTNET_ROOT и готовыми FDD assets. Producer использует
SDK; execution не вызывает build/restore/source preparation. Проверяются игровые
ticks, настоящий World/typed storage/phase commands и stop/dispose ownership.

[`PlayerProbe`](../../ManualTests/PlayerProbe/README.md) отдельно проверяет native/GPU
source/publish/fresh profiles, Playing fence, resize/minimize/restore и cleanup.
Наличие game DLL фиксируется во время игры; её отсутствие после Unload не является
ошибкой активации. ARCH3.6 добавляет отдельный managed [ModHost/SDK/manifest loader](MODS.md)
с group-owned private Worlds, cleanup retry и weak-reference collection proof.
GameBuildLoader остаётся прежним game-session путём. Hot replacement/state migration,
другие OS/RID, AOT/trimming, device loss и general release остаются следующими
этапами. Точные текущие результаты — в STATUS/HISTORY.


## ARCH3.7 stable-component system replacement

[ModSystemSession](MOD_RELOAD.md) предоставляет scoped managed replacement с
capture/restore, callback/tick fences и explicit rollback/retirement cleanup.
World остаётся caller-owned; частные component types используют отдельный
[owned-World migration профиль ARCH3.8](MOD_MIGRATION.md). Исторические
RuntimeConsumer/Player proofs выше не подменяют новую ARCH37 приёмку.
