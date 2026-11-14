# TrueMoon.Aether.Tooling

Самостоятельный headless слой Aether: project/document coordination,
authoring/history/persistence, SDK/compiler/capture, source watch, preview
coordination и диагностический export. Не зависит от Editor/Alloy/Argentis.

```csharp
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Hosting;

context.UseAetherTooling(options =>
{
    options.HostContractId = targetHostIdentity;
    options.BuildDirectory = absoluteBuildDirectory;
});
```

App владеет ToolingHost и EditorProjects. Host единолично владеет compiler product,
coordinator — project/document/source products. После StartAsync composition root
передаёт host/borrowed services в handlers. `host.Projects.OpenAsync(...)` возвращает
project с Documents: Load/edit/Undo/Redo/Save. `host.BuildAsync(revision)` выбирает
LastGoodBuild и source baseline только при успехе и актуальном project opening.
Ошибка/отмена не заменяет last-good. Build не запускает игру.

UseConfiguredAetherTooling использует общий App profile с точным
ToolingConfigurationSchemas.Settings. Identity/RuntimeAccess остаются явными в
ToolingComposition. Headless game ABI по умолчанию нейтрален. Normal transitions
требуют Save/Discard/Cancel; terminal App Stop явно discards несохранённую память
после compiler drain. Cleanup failure сохраняет владельцев для retry.

Прежний полный UseAether/AetherHost workflow теперь также в Tooling; он явно
подключает Runtime и взаимоисключающий с headless aggregate в одном App. Editor
пока использует full workflow; его UI/optional bridge разделяются в ARCH3.5.

[Границы и миграция](../docs/engine/TOOLING.md), [package guide](PACKAGE.md),
[independent proof](../ManualTests/ToolingConsumer/README.md).
