# Минимальный Player

Windows x64 Player-only host; зависимости: `TrueMoon` и `TrueMoon.Aether.Player`.
Версия SDK/TFM/runtime-async берётся из общего build profile.

```powershell
dotnet run --project Examples/AetherPlayer -- --artifact <delivered-build-directory> --scene <scene.aetherscene>
```

`--player` optional: это default и единственная роль данного executable.
`--help` перечисляет window/font/report options. Формат по умолчанию — полный
scene World JSON. Старый ограниченный SceneSnapshot требует явного
`--scene-format snapshot`: `{ "formatVersion": 1, "id": "scene", "revision": 1, "entities": [] }`.

Build должен быть опубликован заранее для **точного entry/shared fingerprint этого
Player** (`BuildIdentity.ForPlayer` в publisher; в запущенном Player `BuildIdentity.HostId`).
Artifact от другого runtime/Editor pair автоматически совместимым не становится.
Для создания artifact используйте существующий `GameBuildArtifact.Publish`, выбрав
готовый output, entry DLL/type, target Player fingerprint и source revision.
Здесь нет скрытого build, поиска SDK или исходников. Инструмент публикации —
отдельная авторская/build операция, не часть запуска игры.

Щелчок захватывает ввод; WASD — движение, E — действие, указатель — bounded look,
Escape/focus loss/resize/minimize снимают capture и очищают held/actions.
Окно начинает Playing только после фактического показа актуального GPU кадра.
Закрытие/Ctrl+C останавливают loop, затем surface/mesh resources и game/World.

[API, ownership и границы приёмки](../../docs/engine/PLAYER.md).
ENG7b.3 завершён в Windows x64 launch/delivery scope: [матрица и границы](../../docs/engine/LAUNCH_ACCEPTANCE.md), [воспроизводимый runner](../../ManualTests/LaunchProfiles/README.md).
