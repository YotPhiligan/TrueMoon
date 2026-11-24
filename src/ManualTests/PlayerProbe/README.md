# ENG7b.1–2 native proof

```powershell
pwsh -NoProfile -File ManualTests/PlayerProbe/Verify.ps1 -ValidationDirectory <existing-khronos-layer-directory> -EvidenceDirectory <fresh-evidence-directory>
```

Использует установленный SDK из global.json и общий `ManualTests/BuildProfile.ps1`.
Собирает выбранный game fixture отдельно. RuntimeArtifactTool в отдельном процессе публикует
artifact для exact Player identity; нормальный Player/PlayerProbe читает готовый artifact
и не зависит от workflow/compiler. Runtime-only граф проверяется отдельно.
Три профиля (source Debug, published Release, fresh package-only Release) выполняются
на Cobalt и Microsoft DI. Fresh consumers расположены вне checkout, имеют offline
feed, отдельный пустой cache, CPM isolation и ни одного ProjectReference.

Для поставки consumer из чистого дерева добавьте `-RepositoryVersion`. Пакеты
получат точный `Version` из профиля сборки. После завершения проверки
`verification.json` фиксирует их пути и SHA-256. Feed находится в
`RunDirectory/feed`. Указанный `-PackageVerification` использует версию и
пакеты уже существующей проверенной поставки.

Каждый профиль фиксирует неизменяемые artifact/scene inputs до App; изменяет scene
file после capture; показывает два cube, независимый tint по W, уменьшение geometry
по единственному E action; resize/minimize/restore; actual presentation/Playing fence;
закрытие и zero remaining surface/slot/handle owners. PNG readback используется
только в пяти диагностических capture, обычные кадры копируются GPU→GPU.
Проверяется game в текущем CLR, отсутствие Editor и process-host services.

Это bounded Windows x64/GPU acceptance минимального Player. Combined role/crash/reap,
выделение остальных Tooling workflows и общий release/device-loss/performance gate — отдельные этапы.

Расширенная ENG7b.3 matrix завершена в [LaunchProfiles](../LaunchProfiles/README.md):
combined/separate hosts, повторные Play/Stop/crash, документы/history, CLI и SDK-free
Player. Этот runner сохраняет отдельный минимальный Player scope.

Для фиксированных тиков, PNG и отчёта используйте `PlayerProbe --scenario`.
Для сверки с headless-профилем используйте `VerifyScenarioProfiles.ps1`.
[Формат сценария и пакетный пример](../../docs/engine/SCENARIO_TESTING.md#headless-player-scenario).

<a id="orthographic-route"></a>
## Ортографический маршрут TrueMoon.Tales

Сначала выполните `Verify.ps1 -RepositoryVersion` с точной версией,
выбранной в игре. Затем используйте полученный `verification.json`.

```powershell
pwsh -NoProfile -File ManualTests/PlayerProbe/VerifyOrthographicRoute.ps1 `
    -PlayerVerification <player-verification.json> `
    -GameSourceDirectory ../../TrueMoon.Tales.Game/src `
    -ValidationDirectory <Vulkan-layer-directory> `
    -EvidenceDirectory TestResults/OrthographicRoute/<fresh-run>
```

Build использует закрытый feed и отдельный пакетный кэш. Player открывает
артефакт с ортографической сценой и сохраняет кадры до и после resize.
Проверка сверяет физический размер, актуальную surface generation,
четыре ориентира, точные ID Build/сцены и счётчики Vulkan после Stop.
Аргумент `-ProbeAssembly` позволяет повторить проверку новым пробником
с теми же пакетными DLL.
