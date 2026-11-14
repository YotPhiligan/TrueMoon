# Editor UI и optional Tooling bridge — ARCH3.5

`TrueMoon.Aether.Editor.UI` содержит Views, styles, presentation data/ports,
viewport control contract, числовую геометрию и Runtime-only App/window adapter.
`TrueMoon.Aether.Editor.Tooling` подключает к этим портам project/document/build,
inspector drafts/history, preview camera/translation и diagnostics/source navigation.
Ни один из пакетов не создаёт собственный App.

| Пакет | Назначение и зависимости |
| --- | --- |
| Abstractions | Нейтральные game/ECS/scene/render контракты и значения |
| Runtime | Prepared loading, World/session/control/frame/backend/resources |
| Tooling | Headless project/document/compiler/authoring/history/source services поверх Runtime |
| Editor.UI | Общие UI View/data/styles/ports; Runtime + Alloy.Hosting; без Tooling |
| Editor.Tooling | Optional full Editor adapters; UI + Tooling |

## Runtime + UI

Composition root явно получает `PlayerStart` из готовой сборки и захваченной сцены.
`PlayerStart.CaptureAsync` — отдельный bootstrap adapter: проверяет artifact/scene
и читает только явно переданные файлы; ничего не компилирует и не загружает код игры.
`FromSnapshot`/`FromWorld` принимают уже подготовленные immutable данные.

```csharp
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Configuration;

// start, fonts, reportDirectory are explicit captured inputs from the host.
var profile = ConfigurationProfiles.Create(builder =>
{
    builder.AddSchema(RuntimeConfigurationSchemas.Engine);
    builder.AddSchema(EditorUiConfigurationSchemas.Window);
    builder.Override("Aether:ReportDirectory", reportDirectory);
});
await using var app = App.Build(context => context.UseConfiguration(profile)
    .UseConfiguredAetherEditorUi(start, new RuntimeComposition
    {
        HostContractId = start.Artifact.HostContractId,
        GameBuildId = start.Artifact.BuildId,
        RuntimeLaunch = runtimeLaunch // explicit native child command
    })
    .UseConfiguredAetherEditorUiWindow(fonts));
await app.StartAsync();
```

`UseAetherEditorUi(start, configureRuntime)` сохраняет typed callback без profile.
Оба пути регистрируют RuntimeEngine, RuntimeEditorSession, RuntimeEditorViewModel,
style и `IFactory<EditorView>`. Повторное подключение и mismatch artifact/composition
отклоняются при сборке App. Identity/factories не являются configuration keys.
`EditorUiConfigurationSchemas.Window` использует общий WindowConfigurationSchema,
prefix `Editor:RuntimeWindow`; это обычное окно. Full Editor сохраняет прежний
`Editor:Window` с custom chrome. Источники/приоритеты выбирает App.

В Runtime-режиме доступны viewport и Start/Play/Pause/Step/Stop/Cancel. Start ждёт
реальную первую презентацию; mesh Playing использует существующий render-view fence.
Выбор и read-only inspection показывают **захваченную сцену**, не живое состояние
World. Полная нейтральная World scene и legacy SceneSnapshot поддерживаются.
Сборка/Replace недоступны; project/document/draft/history/diagnostic panels не создаются.
Unavailable action ничего не запускает. Камера принадлежит игре; authoring camera/
translation предоставляются только full adapter. Нового live World editing API нет.

## Full Editor и порты

Composition использует `TrueMoon.Aether.Editor.Tooling.UseAetherEditor` /
`UseConfiguredAetherEditor` и прежние window/extensions в новом namespace.
Views и styles импортируются из `TrueMoon.Aether.Editor.UI.*`.
Существующие concrete ViewModels реализуют шесть UI-owned INPC ports:
`IEditorViewModel`, `IEditorStatusBarViewModel`, `IEditorDocumentViewModel`,
`IEditorHierarchyViewModel`, `IEditorInspectorViewModel`, `IEditorDiagnosticsViewModel`.
Порты не содержат Tooling types, resolver или handles. UI-owned decisions
`EditorDocumentCloseChoice`/`EditorEntityPlacement` преобразуются явно в прежние
Tooling decisions с проверкой enum. Concrete adapter APIs остаются доступны.

`IEditorViewportController` создаётся через typed factory с EditorViewportArguments;
View владеет controller и input capture. Full controller сохраняет document epoch,
content/selection/layout generation fences, camera state, drafts и Undo/Redo.
TranslationTarget захватывает authoring identity и остаётся в bridge; чистая
TranslationGeometry/Drag/Position находится в UI. Общая GPU binding зависит только
от FrameSession и заимствованного окна, без AetherHost/Tooling.

## Владение и embedded host

App владеет Runtime/Tooling services и concrete ViewModels. Alias на disposable
models не добавляется: View заимствует port, снимает подписки при Dispose и не
освобождает model. Фабрика создаёт View на UI owner thread; окно/родитель владеет
результатом. Удаление одного View не останавливает заимствованный App.

Embedded Runtime host получает `IFactory<EditorView>`, владеет View/UiSession и
после реальной презентации вызывает `RuntimeEditorSession.Present(ui, view)`.
Одна App допускает один live presenting Runtime View; сначала Dispose прежнего.
Для общего GPU transport вызывается `InitializeViewportAsync` с живым HostedUiWindow;
собственный transport задаётся через RuntimeOptions и сам определяет доставку кадров.
Full host использует прежние `EditorSession.Present`/`RequestCloseAsync`; сохраняется
Save/Discard/Cancel policy. `UseAetherEditorUiWindow` и full window adapter — optional.

RuntimeEditorSession закрывает admission, отменяет и дожидается commands/ack,
останавливает Frames и отвязывает UI. Standard runtime window делает это до native
window Stop, пока GL context жив. App остаётся владельцем RuntimeEngine; session
не Dispose заимствованный engine. Cleanup failure остаётся видимым и retryable.
Terminal Stop запрещает поздние команды. UI model обновляется только на owner thread,
не выполняет IO/Resolve в frame loop и не перестраивает дерево при status refresh.

## Миграция и проверки

Старый `TrueMoon.Aether.Editor` проект/package выведен из текущего wiring, facade и
type-forwarding не предоставляются. PackageReference меняется на Editor.Tooling для
full workflow либо на Editor.UI для Runtime-only. Imports разделяются по таблице.
Нужен coherent rebuild hosts/games/artifacts/captures из-за assembly identity/SHA;
общая версия, schema2, scene/project/control/frame/checkpoint formats не менялись.
Исторические net10 и прежние manifests не переписываются.

Воспроизводимый Runtime-only proof: `ManualTests/EditorUiConsumer/Verify.ps1`.
Он проверяет source Debug, published Release, fresh-package и SDK-free profiles
на обоих DI: UI selection/inspection, real mesh pixels/presentation, Step/Play/Pause,
Stop/reap, native owners, exact package graph без Tooling, isolated cache/feed.
Artifact publisher с Tooling выполняется отдельно **до** runtime execution.
SDK-free — копия dotnet host/runtime без sdk, системная установка не изменяется.
Full package/GUI workflows остаются в `ManualTests/AetherPackageConsumer`.
Фактические результаты и ограничения записываются в [STATUS](STATUS.md) и HISTORY;
это не Mod SDK/reload, release acceptance, AOT или другие OS.
