# Границы пакетов Aether и Editor

ARCH2a–c,2026-10-27. [Этапы и критерии ARCH2](PLAN.md#arch2-aether-и-editor-как-пакеты--2026-10-27),
[текущие проверки](STATUS.md). Эта карта описывает размещение и ownership;
подробный roadmap остаётся в PLAN.

## Общие API движка

| Типы/область | Assembly и namespace | Lifetime/owner |
| --- | --- | --- |
| Entity/component schemas, SceneDocument, resolution | TrueMoon.Aether; TrueMoon.Aether.Scenes | Immutable values; shared stateless resolution заимствуется из App |
| ISceneAuthoringSession, mutations/query/revisions/history | TrueMoon.Aether; TrueMoon.Aether.Scenes.Authoring | Factory product принадлежит caller/document session; factory и resolution borrowed |
| SceneFileCodec, ISceneDocumentStore, JsonSceneDocumentStore | TrueMoon.Aether; TrueMoon.Aether.Scenes | Shared App services, borrowing authoring factory; validation products и per-call streams закрываются внутри операции |
| SceneFile, SceneFileStamp, SceneFileConflictException | TrueMoon.Aether; TrueMoon.Aether.Scenes | Значения результата/ошибки persistence; не содержат UI/project/session owner |
| Runtime/preview/build/checkpoint/Replace/frames/process coordination | Существующие подсистемы TrueMoon.Aether | Existing App/factory/session owners; relocation не меняет runtime/game state |
| NativeRuntimeLaunch, RuntimeLaunchIdentity, BuildIdentity, RuntimeBootstrap | TrueMoon.Aether; TrueMoon.Aether.Hosting | Immutable command/counterpart paths и validated launch values; stateless identity/parsing до создания child App. Main и process/report composition принадлежат executable |

Общие file APIs перенесены из Editor.Documents в Aether.Scenes. Движок по-прежнему
не получает reference на Editor/Alloy/bootstrap implementation; сериализация,
validation и file IO доступны через Aether без открытия Editor.

## Что остаётся в Editor

| Existing область | Решение ARCH2a |
| --- | --- |
| Documents: SceneDocumentSession/State, DocumentCommand, EditorDocumentCommands и View/VM/style | Editor владеет открытым документом, IO admission/close policy, saved checkpoint/dirty UX и authoring product; borrowed Aether store/factory не освобождает |
| Projects: EditorProjects/ProjectSession, project manifest/store/toolchain | Прикладной выбор проекта, paths/toolchain preparation и ownership открытых document sessions остаются в Editor. Runtime получает выбранный artifact/launch, не обязан читать Editor project manifest |
| Editing/Hierarchy, selection, EditorWorkspace/Commands | UI drafts, gesture/command availability, project/document/preview coordination остаются в Editor; committed mutations/history вызываются в Aether |
| View/Data/ViewModel/IViewStyle, header/status/popups/property controls | Reusable Editor package layer; UI products принадлежат parent/window, dependencies borrowed; создание на UI owner thread |
| EditorViewport/Binding и EditorPreview | Adapters между UI presentation и Aether preview/frame contracts остаются в Editor |
| Configuration extensions | Editor registrations остаются в Editor; общий codec/store регистрируются Aether extension. Aggregate UseAetherEditor и optional UseAetherEditorWindow реализованы в ARCH2b |
| Program/EditorApplication/EditorLaunchOptions/ProjectEntryPoint и smoke checks | Перенесены в ManualTests/AetherEditorHost, namespace TrueMoon.ManualTests.AetherEditorHost; пакет не зависит от executable. Оставшаяся demo/asset cleanup — ARCH2d |
| RuntimeEntryPoint/EngineChild/BuildIdentity | Main/App.Build/diagnostics/report остаются в executable hosts. Aether.Hosting предоставляет explicit NativeRuntimeLaunch, shared RuntimeBootstrap и counterpart fingerprint; runtime implementation/owned process coordination остаются в Aether (ARCH2c) |

Editor теперь Library/IsPackable=true, refs — Contracts/Aether/Alloy.Hosting без
concrete TrueMoon bootstrap. PACKAGE.md включён в local NuGet; entry point и
executable payload отсутствуют. Explicit runtime launch/bootstrap/identity реализованы
в ARCH2c; полная host/asset delivery и fresh independent NuGet/published acceptance
остаются ARCH2d/e.

## Editor App integration ARCH2b

UseAetherEditor(configureEngine, configureEditor) подключает engine, authoring,
documents/projects, shared models/styles и IFactory<EditorView>. Options копируются;
повторное aggregate attachment и ранее подключённый engine/project graph отвергаются.
Initial ProjectPath открывается при App startup до окна; empty workspace не требует
SDK/game/window. Existing stores/styles/root model/root view factory сохраняются.
Для custom graph остаются прежние lower-level extensions.

EditorSession заимствует services, обновляет View через Present на UI owner thread
и координирует close/drain. Lifecycle aliases не disposable, единственные owners —
DI для services, project для document/authoring, parent/window для View. Failed
startup/creation/cleanup сохраняют исходную ошибку; retry cleanup проверен отдельно.
Viewport binding использует weak UI references, не удерживает закрытое дерево.

UseAetherEditorWindow(fonts, configureWindow) — optional стандартный Windows/OpenGL
adapter с Header и close policy. Host предоставляет font catalog; registration
не создаёт backend/window. EditorWindow владеет только subscriptions и своей
cancel/drain initialization task. Для embedded UI host создаёт View через factory,
вызывает EditorSession.Present/RequestCloseAsync и владеет UI. При custom viewport
transport не ожидается готовность стандартного окна; default GPU bridge требует его.
Новых scopes/async activation APIs не введено.

CLI EditorLaunchOptions больше не входит в Editor public package API. Existing
consumer запускает AetherEditorHost.exe или собственный Main; прежний --runtime
branch явно выбран через CurrentProcess("--runtime") и остаётся совместимым профилем
до host/resources stage, не implicit требованием к consuming exe. Команды и ownership —
в [README host](../../ManualTests/AetherEditorHost/README.md), reusable API —
в [PACKAGE.md](../../TrueMoon.Aether.Editor/PACKAGE.md).

## Native launch и bootstrap ARCH2c

Parent composition задаёт managed apphost и runtime DLL абсолютными путями:

```csharp
engine.RuntimeLaunch = new NativeRuntimeLaunch(absRuntimeExe, absRuntimeDll);
engine.HostContractId = BuildIdentity.ForRuntime(absRuntimeDll);
```

Для dotnet: `new NativeRuntimeLaunch(absDotnetExe, absRuntimeDll,
[absRuntimeDll, .. runtimeArguments])`. Descriptor immutable, arguments копируются
как отдельные значения без shell parsing; implicit executable/`--runtime` больше нет.
Null `AetherOptions.RuntimeLaunch` разрешает registration/headless/common services
и custom `RuntimeFactory`; default native Start сообщает понятную configuration error.
Standalone вариант — `new NativeRuntimeFactory(runtimeLaunch)`. Explicit
`CurrentProcess("--runtime")` требует собственного handler и сохраняет прежний HostId.

HostContractId для separate runtime — labelled host/runtime managed-entry pair плюс
exact shared Aether/Contracts/Enerit/Mithril binaries. Проверяются shared binaries
в обоих entry directories и actual managed entry на parent/child sides. Version1
RuntimeLaunchIdentity передаётся в bootstrap; bounded JSON≤16384characters/depth32.
Control3/frame1/snapshot2, session/nonce/generation fencing не изменены.
Это trusted-local compatibility check, не authentication boundary и не полный hash
native/private dependencies. Deployment не меняют in place во время session.
Разные pair identities инвалидируют старые game artifacts/checkpoints: rebuild games.
Scene/project formats не меняются.

Child executable вызывает `RuntimeBootstrap.ReadEnvironment()` до App construction,
из `.Launch` настраивает UseAether и регистрирует UseAetherRuntime либо
UseAetherPreviewRuntime с `.ParentProcessId`. Strict profile — selected game или
built-in preview; legacy Game.Logic probe и pre-dispatch classification явно
включают allowLegacyGame:true. Child-only App launcher не нужен. Parser/validation
общие, но Main/App.Build/diagnostics/report остаются в consumer, не в library.

[ManualTests/AetherRuntimeHost](../../ManualTests/AetherRuntimeHost/README.md) — отдельный
runtime без Editor/UI dependencies. Existing AetherEditorHost пока явно использует
same-exe профиль до resources stage. Новый EngineSessionProbe CLI
`dotnet <EngineSessionProbe.dll> --runtime-launch-proof <runtime dll/exe path> <fresh report dir>`
проверен отдельно source Debug и published Release, DLL/apphost4/4.
[Evidence и ограничения расширенной matrix](../../TestResults/ARCH2c-20261027/verification.json).
Этот headless process scope не доказывает asset delivery, GPU/presentation или
independent NuGet acceptance; ARCH2d/e остаются открытыми. [API](../../TrueMoon.Aether/PACKAGE.md),
[proof scope/CLI](../../ManualTests/EngineSessionProbe/README.md#arch2c-explicit-runtime-launch-и-independent-process-proof).

## Подключение scene persistence без Editor

```csharp
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Scenes;
using TrueMoon.Dependencies;

await using var app = App.Build(context =>
{
    context.UseAetherAuthoring();
    context.UseAetherScenePersistence();
});
await app.StartAsync();
var store = app.Services.Resolve<ISceneDocumentStore>()!; // composition root
var loaded = await store.ReadAsync(scenePath, cancellationToken);
var stamp = await store.WriteAsync(scenePath, loaded.Document, loaded.Stamp, cancellationToken);
```

Прикладные services получают ISceneDocumentStore через constructor injection.
UseAetherScenePersistence требует authoring factory; его можно предоставить через
UseAetherAuthoring либо override registration. Codec/store defaults создаются лениво,
existing registrations сохраняются, repeated attachment idempotent. Ни registration,
ни App startup не открывают files, authoring sessions, window/GPU/runtime.

UseAetherEditorDocuments делегирует общую persistence registration этому extension
и создаёт только Editor document factory/registration marker. Его повторное
подключение по-прежнему отвергается. Можно явно подключить engine persistence
раньше Editor; custom codec/store и document factory сохраняются.

## Совместимость переноса

Alpha API namespace для шести типов меняется с TrueMoon.Aether.Editor.Documents
на TrueMoon.Aether.Scenes: SceneFileCodec, ISceneDocumentStore,
JsonSceneDocumentStore, SceneFile, SceneFileStamp, SceneFileConflictException.
Consumers должны обновить using/fully qualified names и ссылаться на Aether package.
Editor-specific document sessions/commands сохраняют прежние namespaces. Compatibility
facades/linked production copies не вводятся; common types имеют одного владельца.

Scene formatVersion1 и deterministic UTF8/BOM policies, IDs/revision/raw catalogs,
4MiB/4096entities/4096definitions/1024schemas bounds сохранены. Store по-прежнему
сравнивает SHA256 exact bytes, использует create-only при expected=null и atomic
replacement с cooperative write-lock. Cancellation проверяется до commit;
невозможно обещать durable fsync или защиту от произвольных внешних rename races.
Перенос не меняет format version и не требует пересохранения existing scenes.
