# Independent Runtime acceptance

```powershell
pwsh -NoProfile -File ManualTests/RuntimeConsumer/Verify.ps1 -EvidenceDirectory <fresh-directory>
```

ARCH3.3: source Debug, published Release, fresh package-only Release и runtime-only
root без SDK; каждый профиль выполняется на Cobalt и Microsoft DI. Общий профиль
SDK/TFM/LangVersion/Features берётся из `../BuildProfile.ps1`; fresh feed/cache и CPM
изолированы. Consumer не имеет workflow/Editor/UI references или DLL в поставке.

Отдельный `RuntimeArtifactTool` публикует заранее собранную neutral ExtensionGame и
готовый `.aetherscene`; выполнение consumer не компилирует исходники. Проверяются
четыре game ticks, revision41/color19, реальный typed ECS с двумя системами и phase
commands, total88/count3, ровно по одному Stop/Dispose, exact shared contracts и
collectible game context. Runtime diagnostics не объявляет compiler availability.

Runtime-only root содержит dotnet host/fxr и Microsoft.NETCore.App нужной версии,
не содержит `sdk`; проверяется пустой `--list-sdks`. Это проверка запуска готовых
FDD assets без SDK в выбранном root/PATH, не удаление SDK с рабочей машины.
Unload request не является доказательством collection или live mod replacement.
Native/GPU проверяется отдельным PlayerProbe. [API/границы](../../docs/engine/RUNTIME.md).
