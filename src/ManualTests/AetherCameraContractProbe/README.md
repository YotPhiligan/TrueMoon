# Численная проверка контракта ED5a

Headless console probe для [camera/selection contract](../../docs/engine/EDITOR_CAMERA_SELECTION.md).
Использует текущие `PerspectiveCamera.CreateViewProjection` и `PrimitiveScene`;
ray/unprojection/cube helpers — prototype будущего picking, не production API.
Проверяет projection compatibility, clip/Y/pixel mapping, DPI/letterbox,
world-distance parameter при scale/shear и rejected camera inputs.

```powershell
dotnet run --project ManualTests/AetherCameraContractProbe -- report.json
```

Путь JSON необязателен; без аргумента отчёт выводится в stdout. Это численная
проверка без native окна, GPU, child runtime или изменения документов. Она не
подтверждает camera transport, actual-present receipt, input или выбор в UI.
Production tests и native acceptance ведутся в [ED5a](../../docs/engine/EDITOR_PLAN.md#ed5a-camera-picking).
