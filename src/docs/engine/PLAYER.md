# Минимальный Aether Player — ENG7b.1–2

`TrueMoon.Aether.Player` — самостоятельная App/UI integration без зависимости от
Editor package. Low-level `UseAetherLocalRuntime(PlayerStart)` находится в Aether и
не требует UI. Сборка Aether пока содержит прежние Tooling/compiler типы;
их физический вынос остаётся ARCH3.3.

## Запуск и конфигурация

`AetherLaunchOptions.ParseAsync` использует общий single-use CLI parser/schema.
Default combined host — Editor, Player-only host — Player; selectors `--editor`,
`--player`, `--runtime` взаимоисключающие. `--runtime` требует защищённый bootstrap.
Даже пустое/некорректное наличие bootstrap/parent/control identity не разрешает
fallback в Player/Editor. Полный `RuntimeBootstrap.ReadEnvironment` остаётся
обязательным self-check child до App. Формат/ownership bootstrap не изменён.

Player требует явные `--artifact` и `--scene`; world JSON — default, legacy
snapshot только с `--scene-format snapshot`. Дополнительно `--width`, `--height`,
`--title`, `--font`, `--report`, `--validation`. Editor принимает `--project` и
`--runtime-host`; Player их отвергает. Unknown/scalar duplicates/positionals,
неверные dimensions, форматы и role-specific options — ошибки до App.
CLI и entry examples: [Player](../../Examples/AetherPlayer/README.md),
[combined host](../../Examples/AetherHost/README.md).

Composition root выбирает источники и priorities. Прямой callback
`UseAetherPlayer(start, fonts, configure)` копирует и проверяет startup options.
`UseConfiguredAetherPlayer` использует **точный** `PlayerOptions.Schema`
(`Aether:Player`) и стандартный prepared profile/App configuration stage;
composition root явно добавляет schema и sources. Artifact/scene/protected
identity не являются обычными пользовательскими overrides. Настройки не читаются
из окружения и не разбираются на каждом кадре.

`PlayerStart.CaptureAsync` проверяет content-addressed artifact и точный host
fingerprint без загрузки game code, затем ограниченно читает/хеширует/валидирует
scene и сохраняет immutable данные. После capture scene file не читается снова.
Перед actual game load artifact повторно валидируется и pin удерживается до
успешного game cleanup. `BuildIdentity.ForPlayer(targetEntry)` позволяет publisher
получить fingerprint доставленного Player с идентичными shared binaries.
Никакого автоматического SDK/build/source/project lookup в Player нет.

## Graph и lifetime

Player использует прежние game entry/`IFactory<IGameSession,Type>`, collectible
loader, SceneSnapshot/SceneWorld и RuntimeProtocol input/tick contracts **локально**.
Не создаёт AetherHost/compiler/project watcher, runtime child/parent handler,
control pipe/server или fake NativeRuntimeLease. World остаётся у game session.
Game logic должна предоставлять primitive либо mesh render capability для окна.

`UseAetherPlayer` регистрирует typed View/data/style и DI factory, standalone
HostedUiWindow, PlayerInput и PlayerWindow. View создаётся UI owner thread,
контролы принадлежат дереву. Shared dependencies заимствуются. В loop нет
container resolution. Окно управляет 60 Hz fixed tick и bounded 30 Hz render;
render extent вписывается в существующий 1920×1080 profile. Zero/tiny extent
приостанавливает rendering и Playing до нового реального показа.

Локальный surface экспортирует Vulkan slots и импортирует их в текущий GL процесс
через настоящий SafeProcessHandle; работает прежняя external-memory/semaphore
синхронизация, ledger и Vulkan retirement fence. Skia удерживает одну независимую
GPU-копию и точный presentation ticket. Upload/production не заменяет actual UI
presentation; mesh допуск проверяет актуальные scene/content/camera revisions.
Resize закрывает старый ticket и дренирует прежнюю generation; явный Pause не
отменяется последующим показом. WASD held, bounded look и E edge очищаются при
Escape/focus loss/suspend/Stop; resize также снимает capture.

Stop сначала закрывает admission/отменяет loop, дренирует local surface и mesh
resources **при живом UI context**, затем освобождает game/World/loader и window.
Closing handler не ждёт собственную window Completion. Failed startup сам
очищает частично созданную game session; failed retirement сохраняет владельцев
и допускает retry. Factory products имеют явного владельца; DI aliases используют
не владеющие AppServiceLifecycle wrappers и не освобождают сервис повторно.

## Проверки и границы

[Воспроизводимый native proof](../../ManualTests/PlayerProbe/README.md),
[история](HISTORY.md#eng7b12-2026-11-12), [manifest/SHA](ENG7B12_BASELINE.json).
ENG7b.3 сохраняет расширенную combined-exe/отдельные hosts/crash/reap/delivery matrix;
этот срез её не закрывает. Нет live переключения Editor↔Player, hot replace модов,
logical content checkpoint, SDK publishing UI, textures/audio/import, device-loss
или общей release/performance acceptance. Исторический ARCH3 inventory требует
дельты этих новых Player типов перед физическим переносом ARCH3.1–5.
