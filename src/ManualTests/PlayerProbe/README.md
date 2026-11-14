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

Каждый профиль фиксирует неизменяемые artifact/scene inputs до App; изменяет scene
file после capture; показывает два cube, независимый tint по W, уменьшение geometry
по единственному E action; resize/minimize/restore; actual presentation/Playing fence;
закрытие и zero remaining surface/slot/handle owners. PNG readback используется
только в пяти диагностических capture, обычные кадры копируются GPU→GPU.
Проверяется game в текущем CLR, отсутствие Editor и process-host services.

Это bounded Windows x64/GPU acceptance минимального Player. Combined role/crash/reap,
выделение остальных Tooling workflows и общий release/device-loss/performance gate — отдельные этапы.
