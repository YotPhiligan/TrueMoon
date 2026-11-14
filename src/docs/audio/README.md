# TrueMoon.Kul: сборка и подключение

Общий аудиомодуль для Windows x64: App/DI, подготовленные звуки, независимые voices
и native streaming музыки. [API/пример](../../TrueMoon.Kul/PACKAGE.md),
[план и ownership](PLAN.md), [проверки и ограничения](STATUS.md).
[Выбор имени и переход с TrueMoon.Audio](NAMING.md). Публичные типы находятся
в `TrueMoon.Kul`; `UseAudio` и конфигурационные ключи `Audio:*` сохранены.

## Сборка исправленной MiniAudioEx

Нужны PowerShell7, Windows x64, MSVC C++ Build Tools + Windows SDK, совместимый
MSVC x64 runtime; .NET SDK берётся из `global.json`. Из src:

```powershell
pwsh -NoProfile -File eng/native/MiniAudioEx/Build.ps1 `
  -OutputDirectory TestResults/AudioNative-build
pwsh -NoProfile -File eng/native/MiniAudioEx/InstallLocalFeed.ps1 `
  -NativeBuildReceipt TestResults/AudioNative-build/native-build.json
dotnet build TrueMoon.Kul/TrueMoon.Kul.csproj -c Debug '-bl:TestResults/audio-build-{}.binlog'
```

Каждый build использует новый output directory. Native source revision, archives,
original/patched source и patch SHA pinned в `eng/native/MiniAudioEx/Source.lock.json`.
Build скачивает/проверяет архивы, накладывает patch в отдельном temp tree, собирает
baseline/patched DLL одним MSVC, сверяет1286exports и упаковывает unchanged managed
assets плюс исправленный win-x64 native. [Полная recipe и native regression](../../eng/native/MiniAudioEx/README.md).
`-SourceArchive`/`-ManagedArchive` позволяют использовать локальные архивы с теми же SHA.

InstallLocalFeed проверяет receipt/archive/native SHA и помещает nupkg в ignored
`eng/native/MiniAudioEx/feed`. Туда направлен **только** exact `TrueMoon.MiniAudioEx`
через package source mapping в NuGet.Config; остальные зависимости остаются nuget.org.
Version хранится в Directory.Packages.props. Binary не коммитится и не опубликован
на nuget.org; пакет нужно предоставить внешнему потребителю через собственный feed.
Нельзя одновременно подключать upstream `JAJ.Packages.MiniAudioEx`: assembly/native
имена совпадают. Оба managed netstandard assets неизменны; лицензии и provenance в nupkg.

Повторная сборка может менять ZIP timestamps и archive SHA, сохраняя exact native bytes
на одном toolset. Installer не заменяет уже установленный ID/version другим архивом:
повторно используйте matching receipt либо отдельный feed/cache для нового артефакта.
Меняя содержимое распространяемого fork, выпускайте новую версию и обновляйте pins/CPM.

## App и потребитель

```csharp
using TrueMoon;
using TrueMoon.Kul;

await using var app = App.Build(context => context
    .UseAudio(options => { options.Volume = .4f; })
    .Services(services => services.Singleton<GameAudio>()));
await app.StartAsync();
// A registered startup/game service receives GameAudio through its constructor.
// Consumers receive IFactory<AudioSession> and IAudioService through DI.
```

`GameAudio` из [package example](../../TrueMoon.Kul/PACKAGE.md). Source consumers,
использующие generated type registrations, подключают Cobalt generator как в других
примерах; factory registration lambda в composition root также поддерживается.
Для headless mixing выбрать `AudioMode.Offline`, затем `IAudioService.Render`.
Для приглушённого device proof поставить `MuteOutput=true`; master/system volume
других приложений не изменяется. Device failure не становится successful offline.

Сначала Start App, затем фабрика sessions. Preparation синхронна: загрузка вне кадра;
native callback не исполняет пользовательский managed код. Dispose voice освобождает
stream/file, Dispose session — весь её звук. App Stop закрывает забытые sessions,
останавливает callback и join workers. Pause/Stop voice оставляют ресурс для replay.
Volume finite0..1, out-of-range inputs/limits отклоняются. Потеря устройства/долгий
driver stall не имеют recovery/deadline в этой версии.
Float32 mixer не нормализует сумму voices и не добавляет limiter; gain/headroom
выбирает потребитель. Полный список ограничений и дальнейшие задачи — в [плане](PLAN.md).

Для common profile добавить exact `AudioConfigurationSchema.Default` и вызвать
`UseConfiguration(profile).UseConfiguredAudio()`. Источники/приоритеты остаются в
composition root; ключи `Audio:Mode`, `SampleRate`, `PeriodFrames`, `Volume`, `MuteOutput`,
`MaximumSessions`, `MaximumClips`, `MaximumVoices`, `MaximumMusicStreams`,
`MaximumSoundFileBytes`, `MaximumResidentSoundBytes`. Это startup-only schema.

## Приёмка

```powershell
dotnet test TrueMoon.Kul.Tests/TrueMoon.Kul.Tests.csproj -c Debug '-bl:TestResults/audio-tests-{}.binlog'
pwsh -NoProfile -File ManualTests/KulModuleProbe/Verify.ps1 `
  -EvidenceDirectory TestResults/AudioModule-acceptance -Device
```

Runner проверяет source Debug, published Release и fresh package-only consumer
на common BuildProfile с private feed/cache. Offline и actual WASAPI различаются;
device output приглушён. [Детали](../../ManualTests/KulModuleProbe/README.md).
Для слышимого запуска сначала согласовать момент прослушивания. Audibility,
systemwide recording и long soak не следуют из muted proof.
