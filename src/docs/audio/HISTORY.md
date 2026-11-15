# Audio: история проверок

<a id="kul-rename-2026-11-12"></a>
## TrueMoon.Kul: переименование и новая приёмка — 2026-11-12

По выбору пользователя модуль, project/PackageId/assembly/root namespace переименованы
из TrueMoon.Audio в **TrueMoon.Kul**; test project/namespace — TrueMoon.Kul.Tests,
manual consumer — KulModuleProbe/TrueMoon.KulModuleProbe. [Решение и migration map](NAMING.md),
[build/API guide](README.md), [новый versioned manifest](KUL_RENAME_RESULTS.json).
UseAudio/UseConfiguredAudio, AudioSession/AudioClip/AudioPlayback, остальные функциональные
Audio-типы и configuration keys Audio:* сохранены. Пространство имён потребителя —
`using TrueMoon.Kul;`, PackageReference — TrueMoon.Kul. Старый совместимый пакет/namespace
не добавлен. Общая версия0.3.0.1-alpha следует Directory.Build.props.

Namespaces изменены Rider Adjust Namespaces с preview:24files основного scope с usages,
5test files и2probe files, conflicts0. После этого явно переименованы каталоги/.csproj,
solution references, InternalsVisibleTo, package consumer recipes и текущая документация.
Корневой README получил Kul entry и first-restore native-feed prerequisite.
Старые JSON/TRX/package hashes и исходные исторические команды ниже не переписывались.

Фактические команды из src:

```powershell
dotnet test TrueMoon.Kul.Tests/TrueMoon.Kul.Tests.csproj -c Debug --logger 'trx;LogFileName=kul-debug.trx' --results-directory TestResults/KulRename-20261112 '-bl:TestResults/KulRename-20261112/tests-debug-{}.binlog'
dotnet test TrueMoon.Kul.Tests/TrueMoon.Kul.Tests.csproj -c Release --logger 'trx;LogFileName=kul-release.trx' --results-directory TestResults/KulRename-20261112 '-bl:TestResults/KulRename-20261112/tests-release-{}.binlog'
dotnet test TrueMoon.slnx -c Debug --no-build --no-restore --filter 'FullyQualifiedName~TrueMoon.Kul.Tests' --logger trx --results-directory TestResults/KulRename-20261112/solution-discovery '-bl:TestResults/KulRename-20261112/solution-kul-{}.binlog'
pwsh -NoLogo -NoProfile -File ManualTests/KulModuleProbe/Verify.ps1 -EvidenceDirectory TestResults/KulRename-20261112/acceptance -Device
```

| Проверка | Фактический результат |
| --- | --- |
| Renamed suite Debug / Release |23passed/0failed/0skipped каждый; все test names имеют prefix TrueMoon.Kul.Tests |
| Solution wiring | Основной TrueMoon.slnx перечисляет Kul/Tests/probe; filtered discovery выполнил23tests, другие suites не запускались |
| Source Debug / published Release / fresh package-only Release | Каждый23offline+18muted actual WASAPI checks; App/DI, PCM,2OGG streams, corruption/seek/crossfade и cleanup |
| Fresh feed |TrueMoon.Kul0.3.0.1-alpha +TrueMoon/TrueMoon.Cobalt/TrueMoon.Contracts той же версии +native fork; пакета TrueMoon.Audio нет |
| Exact loaded module |TrueMoon.Kul.dll SHA0307DFDE10879E676EC106D5606A2298926EE9FD711B8C998B2AE58AA605EF0F совпадает с nupkg asset |
| Native dependency |Прежний TrueMoon.MiniAudioEx4.0.1-truemoon.1, DLL SHAF2F6333F915E08673551C03CA5818F3136BE0B138BD507FB86C878D4A3EE60CB в каждом процессе |
| Cleanup |Final sessions/clips/voices/streams/resident counters0 и files exclusive-reopen; native owners0 в tests |

SDK11.0.100/net11.0/latest/runtime-async=on, source package version из общего профиля,
private feed/cache и CPM isolation. Raw stdout/stderr/TRX/binlogs/nupkg остаются в
ignored local TestResults без автоматического удаления; versioned manifest сохраняет
команды, scope, source/report/package SHA. Native callback/codec/resource behavior
не менялся. Existing dependency warnings остаются; full solution build/regression,
historical native RED/GREEN и слышимость не проверялись заново. Внешней публикации нет.

Приведённая ниже запись AUD1 относится к прежнему имени TrueMoon.Audio. Её команды
сохранены как история; для текущего checkout использовать guide/команды Kul выше.

<a id="aud1-2026-11-12"></a>
## AUD1 Windows framework module — 2026-11-12

По запросу пользователя создан `TrueMoon.Audio` на исправленном MiniAudioEx:
App/DI, decoded sounds, music streaming и корректное владение ресурсами.
[API](../../TrueMoon.Kul/PACKAGE.md), [build guide](README.md), [решения](PLAN.md),
[versioned manifest/SHA](AUD1_RESULTS.json). Общая версия пакетов0.3.0.1-alpha,
SDK11.0.100/net11.0/latest/runtime-async=on через общий профиль.

Публичные точки: `UseAudio` / `UseConfiguredAudio`, borrowed `IAudioService`,
`IFactory<AudioSession>`, `AudioClip` и `AudioPlayback`. Один host принадлежит DI,
factory products caller-owned и дополнительно отслеживаются для App shutdown.
Ранняя очистка удаляет products из host; aliases не владеют повторным Dispose.
Native callback полностью native. SafeHandle children удерживают parent resources,
при Stop admission закрывается перед device drain/voices/engine/worker join/context.

Использован **exact ранее собранный** native package
`TrueMoon.MiniAudioEx4.0.1-truemoon.1`, archive SHA
9C696B9293932D6D09F85DF523D675D7780D7B30D505E3B487674E6C5F1F6955,
native SHA F2F6333F915E08673551C03CA5818F3136BE0B138BD507FB86C878D4A3EE60CB.
Исторический [native RED/GREEN](../engine/HISTORY.md#audio-native-fix-2026-11-12)
не повторялся этой module-задачей. В каждом новом source/published/package процессе
проверен фактически загруженный native SHA. Документация сборки уже существовала;
добавлены receipt-verified local-feed installer и module build/App guide.
Root NuGet source mapping направляет только exact fork ID в ignored локальный feed;
CPM использует отдельную third-party version, общая framework version не менялась.

Фактические финальные команды из src:

```powershell
dotnet test TrueMoon.slnx -c Debug --filter 'FullyQualifiedName~TrueMoon.Audio.Tests' --logger trx --results-directory TestResults/AudioModule-20261112/solution-discovery '-bl:TestResults/AudioModule-20261112/solution-audio-tests-{}.binlog'
dotnet test TrueMoon.Audio.Tests/TrueMoon.Audio.Tests.csproj -c Release --logger 'trx;LogFileName=audio-release-final.trx' --results-directory TestResults/AudioModule-20261112 '-bl:TestResults/AudioModule-20261112/tests-release-{}.binlog'
pwsh -NoLogo -NoProfile -File ManualTests/AudioModuleProbe/Verify.ps1 -EvidenceDirectory TestResults/AudioModule-20261112/final-01 -Device
```

| Проверка | Фактический результат |
| --- | --- |
| Module suite Debug / Release | **23passed /0failed /0skipped** каждый. Debug обнаружен через основной TrueMoon.slnx, Release — direct project |
| Solution discovery | Exit0, build графа solution успешен; фильтр выполнил только аудиотесты. Остальные11suites не запускались, их historical passing counts не перенесены |
| Module source build |0own warnings/errors; unrelated XML/nullability/xUnit warning backlog solution/dependencies сохранён |
| Source Debug |23offline+18actual WASAPI checks |
| Published Release FDD |23offline+18actual WASAPI checks |
| Fresh package-only Release FDD |23offline+18actual WASAPI checks, private feed/cache, CPM isolation/common BuildProfile; без ProjectReferences, exact packaged module DLL SHA |
| Native output | Stereo48kHz, actual WASAPI negotiated periods; SFX и2OGG streams, pause/resume/seek/crossfade. Output muted только своего stream, системная громкость не менялась |
| Streaming и invalid input | Unit suite:128malformed decoded/music load failures суммарно на header/truncation/CRC/WAV, затем valid OGG; в каждом probe profile24malformed OGG failures и valid playback после них |
| Lifecycle |32active stream/session Dispose cycles/profile, partial-start failures после resource manager и engine, later consumer startup rollback, duplicate attach/options snapshot, both DI и concurrent controls/Stop |
| Long file |10-minute WAV в каждом offline profile:115200044encoded bytes,230400000decoded float bytes, seek/loop. Unit test mono variant дополнительно проверяет process-private delta≤32MiB; это bounded evidence, не полный native allocation audit |
| Cleanup | Final session/clip/voice/stream/resident counters0, files exclusive-reopen после release и App Stop; native owner counters0 в module suite |
| Package delivery | Fresh4framework nupkg:TrueMoon.Audio/TrueMoon/TrueMoon.Cobalt/TrueMoon.Contracts плюс exact native fork. Module DLL SHA D7908E405118D97756365CC1BF634A61FED54320A3B17BABDEE0F6403BA1B6AF |

Сначала два stream-seek теста ожидали PCM немедленно, но native refill выполняется
на resource-manager workers. Тесты проверяют возвращение фактического PCM в bounded
wait, контракт явно описывает временную silence. Прежние неполные attempts сохранены
в TestResults отдельно, таблица относится к финальным runs. В native handle constructor
сначала выделяется массив родителей, затем выполняются AddRef, чтобы allocation failure
после AddRef не терял ownership; startup error и rollback error сохраняются вместе.

| Требование пользователя | Конкретное свидетельство |
| --- | --- |
| «с подключением через App/DI» | `AudioAppTests.AppOwnsOneHostAndDrainsForgottenSessions`, `ConfiguredAudioUsesCommonProfileAndExplicitOverride`; `NativeLifetimeTests.MicrosoftAdapterBorrowsAliasesAndDrainsOneNativeOwner` |
| «воспроизведением звуков» | `AudioPlaybackTests.SoundVoicesMixIndependentKnownPcmAndRetainClipUntilLastRelease`, `PlaybackPauseResumeSeekStopAndGainAffectActualPcm` |
| «стримингом музыки» | `AudioStreamingTests.OggMusicStreamsSeeksPausesAndClosesItsFile`, `TenMinuteMusicUsesStreamingInsteadOfResidentWholeFilePcm` |
| «корректным освобождением ресурсов» | `NativeLifetimeTests.NativeConstructionFailureRollsBackInitializedParents`, `ConcurrentControlsSessionDisposeAndAppStopDoNotUseFreedNativeMemory`; `AudioStreamingTests.MusicLimitReturnsCapacityAndRepeatedSessionDisposalClosesStreams` |
| «создай документацию по сборке исправленной версии, если её ещё нет» | [Native recipe](../../eng/native/MiniAudioEx/README.md), [local feed installer](../../eng/native/MiniAudioEx/InstallLocalFeed.ps1), [module build guide](README.md); относительные ссылки проверены |

Raw TRX/binlogs/stdout/stderr/fixtures остаются в ignored local TestResults без
автоматической очистки. Versioned manifest сохраняет команды/scope/outcomes/hashes;
отсутствие raw artifacts на другом checkout не заменяет повторную проверку.
Packages локальные, внешней публикации/commit/push не было.

AUD1 не реализует ECS/Nom/captured-asset bridge, device-loss recovery, async command
queue, hard transient/stream-page cap, limiter, spatial audio, AOT или другие OS.
Синхронная preparation выполняется вне frame/audio callback; native drain нельзя
принудительно прервать driver timeout. Нового audible подтверждения не было.
Перечисленные расширения остаются в AUD2–3 и engine adapter плане.

<a id="audio-status-before-kul-ecs-2026-11-15"></a>
## Previous audio STATUS before Kul ECS — 2026-11-15

# Kul / Audio: состояние

**2026-11-15:** [общий session-subsystem слой Aether](../engine/SESSION_SUBSYSTEMS.md)
реализован и проверен отдельно. Kton PHYS5 также завершён; следующий согласованный этап —
Kul ECS/captured-assets adapter. Конкретный audio bridge ещё не реализован. Новых Kul
backend/device/слышимых проверок эта работа не добавляет.

**2026-11-14 — предкоммитная сверка:**23tests D/R, source/published/fresh delivery
по23offline checks и package metadata прошли. Device/слушание и native rebuild
не повторялись. [Состав, команды и границы](../COMMIT_AUDIT_2026-11-14.md).

**2026-11-12 — переименование в TrueMoon.Kul завершено и проверено.**
Project/PackageId/assembly/root namespace — `TrueMoon.Kul`, тесты — `TrueMoon.Kul.Tests`,
consumer — `KulModuleProbe`. [Выбор имени/переход](NAMING.md), [сборка/App API](README.md),
[история команд](HISTORY.md#kul-rename-2026-11-12), [новый manifest/SHA](KUL_RENAME_RESULTS.json).
UseAudio/UseConfiguredAudio, AudioSession/AudioClip/AudioPlayback и ключи Audio:* сохранены.

Новые проверки Kul: **23passed/0failed/0skipped каждый Debug/Release**;23tests также
найдены через основной TrueMoon.slnx без сборки чужих проектов. Source Debug/published
Release/fresh package-only Release прошли каждый23offline+18muted actual WASAPI checks.
Final logical owners/resident bytes0, files closed; exact new Kul DLL/package SHA проверен.
Native MiniAudioEx package/binary прежние, его original RED/GREEN этим rename не повторялся.

Предыдущий [AUD1 под именем TrueMoon.Audio](HISTORY.md#aud1-2026-11-12) и
[его manifest](AUD1_RESULTS.json) сохранены с исходными именами/SHA. Full solution regression
и слышимое прослушивание не повторялись; warning backlog зависимостей остаётся отдельно.

Следующий шаг: требования engine ECS/asset adapter отдельно от самостоятельного модуля;
AUD2 — async preparation/admission, device loss/soak и hard peak budgets по workload.
OGG duration unavailable, synchronous preparation/drain и возможная silence после seek
остаются явными ограничениями. Linux/macOS — AUD3; физическая слышимость повторно не проверялась.

<a id="kul-ecs-2026-11-15"></a>
## Kul scene consumer — 2026-11-15

Nonspatial ARCH1h.2a выполнен в engine adapter, не в самостоятельном AUD2–3.
[Единственная детальная запись/команды](../engine/HISTORY.md#kul-ecs-2026-11-15),
[контракт](ECS_BRIDGE.md), [manifest/SHA](KUL_ECS_RESULTS.json).
Additive StartPaused/RetainAtEnd и AudioPlayback.Loop сохраняют defaults; native
module suite27D/R и adapter103D/R прошли. Independent4offline×59 и4muted actual
WASAPI×43checks с обоими DI; backend binary прежний, native rebuild/слушание отдельно.

<a id="audio-status-before-dep1-2026-11-15"></a>
## Previous audio STATUS before DEP1 — 2026-11-15

# Kul / Audio: состояние

**2026-11-15 — optional Aether ECS/session adapter ARCH1h.2a реализован и проверен.**
[API/границы](ECS_BRIDGE.md), [engine задача](../engine/PLAN.md#arch1h2a-kul-session),
[полная история/команды](../engine/HISTORY.md#kul-ecs-2026-11-15), [manifest/SHA](KUL_ECS_RESULTS.json).

`TrueMoon.Aether.Kul` — neutral SDK без Runtime/native; `TrueMoon.Aether.Kul.Runtime`
связывает captured files с caller-owned AudioSession. Sound clips/fixed music voices
готовятся до publication; actual Play/Pause/Stop и silent paused Step контролируются
общей session. Пулы удерживаются после EOF, commands monotonic, source gain/loop/seek
и entity generation/rebind имеют явные правила. Cleanup: native voices/streams → pins → World.

Самостоятельный Kul получил additive StartPaused/RetainAtEnd options и mutable
AudioPlayback.Loop. Старые defaults/collect-on-admission сохранены. Backend/native DLL
`TrueMoon.MiniAudioEx4.0.1-truemoon.1` прежние; native rebuild не выполнялся.

Новые проверки:103adapter tests и27Kul tests каждый Debug/Release, failed/skipped0;
103cases найдены через точный solution.104common+68Kton regression каждый D/R.
Source Debug/published Release/fresh NuGet Debug/published Release прошли каждый
59offline checks и отдельно43muted actual WASAPI checks, оба DI; final sessions/clips/
voices/streams/resident bytes0, files unlocked.3package metadata/payload checks;
exact managed/native SHA и closed package-only graph проверены. Solution D/R0errors;
non-incremental warning backlog1570/config отдельно от новых пакетов.

Предыдущие AUD1/rename/precommit результаты сохранены в
[истории](HISTORY.md#audio-status-before-kul-ecs-2026-11-15) со своими датами и manifests.
Это не новый full framework regression, слушание или повтор original native RED/GREEN.

Следующий audio этап — AUD2: async admission/preparation, device-loss/long workload и
hard peak/RT budgets по отдельному запросу. AUD3 — другие OS, затем spatial.
Editor import/Nom, automatic GameBuildArtifact/ALC extension и native audio-state
migration отдельно. OGG length unavailable и временная silence при seek сохраняются.

<a id="dep1-2026-11-15"></a>
## DEP1 dependency refresh — 2026-11-15

Новая полная D/R проверка, six-package version refresh и согласованная native Skia4.156.1 поставка выполнены в [единственной межмодульной записи](../core/HISTORY.md#dep1-2026-11-15). Прежние manifests и функциональные gates сохранены; точный scope нового запуска — в записи Core.
