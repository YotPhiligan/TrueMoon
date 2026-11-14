# MiniAudioEx Windows prototype

Изолированный binding prototype для [ARCH1h.0](../../docs/engine/PLAN.md#arch1h-audio).
Не подключается к solution, App/DI, ECS, UI или production packages. Прямые native
owners здесь исследовательские; они не заменяют публичный UseAudio/factory lifecycle.

## Запуск

PowerShell 7, Windows x64, установленный SDK общего профиля из `global.json`.
Запускать из `src`; каждый раз выбирать новый каталог evidence.

Текущий исправленный профиль использует локальный fork `TrueMoon.MiniAudioEx
4.0.1-truemoon.1`. [Native recipe](../../eng/native/MiniAudioEx/README.md)
собирает библиотеку и пакет, затем запускает этот consumer без decoder guard:

```powershell
pwsh -NoProfile -File eng/native/MiniAudioEx/Verify.ps1 `
  -EvidenceDirectory TestResults/AudioNativeFix -Device
```

По умолчанию device output этого wrapper приглушён. Для согласованной проверки
на слух добавить `-Audible`. Для offline-профиля убрать `-Device`.
Проверяются исходная DLL как RED control, исправленная DLL, неизменность managed
assets и размеров всех 68 native allocation types. В Debug и published Release
прежние defect cases должны завершаться успешно; дополнительно выполняются
64 отказа на повреждённых данных с проверкой освобождения файлов и allocations,
затем воспроизведение корректного OGG. `PatchedDependency=true`,
`DependencyProfileVerified=true`, `KnownDefects=[]` обозначают этот профиль.

### Воспроизведение прежнего upstream-дефекта

Следующие команды сохраняют исторический профиль **неисправленного**
`JAJ.Packages.MiniAudioEx 4.0.1`; для нового аудиомодуля использовать fork выше.

```powershell
# Offline, native cleanup и negative controls; устройство не открывается.
pwsh -NoProfile -File ManualTests/MiniAudioExResearch/Verify.ps1 `
  -EvidenceDirectory TestResults/AudioPrototype-offline

# Offline + actual WASAPI device, но gain только собственного output stream равен нулю.
pwsh -NoProfile -File ManualTests/MiniAudioExResearch/Verify.ps1 `
  -EvidenceDirectory TestResults/AudioPrototype-muted -Device -MutedDevice

# Слышимое воспроизведение: примерно по 10 секунд в Debug и published Release.
# Запускать, когда слушатель готов. Системная громкость не меняется.
pwsh -NoProfile -File ManualTests/MiniAudioExResearch/Verify.ps1 `
  -EvidenceDirectory TestResults/AudioPrototype-audible -Device
```

Runner получает SDK/TFM/LangVersion/Features через общий `ManualTests/BuildProfile.ps1`,
копирует исходники в отдельный consumer, создаёт CPM isolation и частный NuGet cache/feed.
Версии и SHA256 обоих архивов зафиксированы в runner. `OggVorbisEncoder 1.2.2`
нужен только для генерации собственной музыки; это не предложение зависимости audio provider.
При прямом запуске без `-PatchedPackage` основной пакет — `JAJ.Packages.MiniAudioEx 4.0.1`.
Для проверки уже собранного fork передать одновременно `-PatchedPackage` и
`-NativeBuildReceipt` из native recipe; в отдельной consumer-копии ссылка на пакет
заменяется, а исходный шаблон сохраняет legacy reproduction.

Все MSBuild-вызовы сохраняют отдельные binlogs. Source Debug и framework-dependent
published Release запускаются в отдельных процессах с дедлайном и reap при timeout.
Fixtures, stdout/stderr, JSON, PCM recordings и `verification.json` остаются в evidence.
Исходные source SHA фиксируются и проверяются перед завершением runner.

## Что проверяется

- PE AMD64/native SHA, scalar ABI и фактическое применение stereo/48 kHz/no-device config.
  Native objects выделяются через библиотечный `ma_allocate_type`; полные native
  structures не копируются в managed buffers. Это не полная сертификация всех P/Invoke.
- Известные PCM: gain, mono→stereo, pause/resume, seek, exact WAV loop, one-shot EOF,
  64 одновременных голоса и кириллица в пути через явный Windows `_w` entry point.
- Ogg Vorbis 44.1→48 kHz, streaming/refill/seek; десятиминутный WAV с ограниченной
  памятью, seek перед EOF и loop. WAV создаётся порциями, без полного PCM в памяти.
- 32 цикла active playback→dispose, повторный Dispose, два независимых engine,
  injected allocation failure/startup rollback, отсутствующий и повреждённый WAV.
- Library-object owner counters, configured native allocator live/peak bytes,
  закрытие stream files через exclusive reopen, post-GC OS handles/private-byte trend.
- При `-Device`: WASAPI shared, SFX и два OGG streams, crossfade, pause/resume,
  seek/EOF, callbacks после stop/drain, forced GC при playback и шесть silent reopen cycles.

Float32 master gain — 0.22. No-pitch/no-spatialization выбран явно; 3D/pitch здесь
не проверяются. Профиль не реализует production command queues, voice admission,
asset identity, Nom bridge, session generations или mod unload.

## Исторические дефекты upstream MiniAudioEx 4.0.1

**Unmodified libvorbis probing не проходит cleanup.** Попытка распознать WAV через
добавленный libvorbis оставляет один наблюдаемый native-блок 840 bytes; malformed OGG
оставляет два таких блока, 1680 bytes суммарно. Отдельные negative-control процессы намеренно завершаются
с exit 1; runner проверяет точную signature и сохраняет её в `KnownDefects`.
Они не суммируются с успешными проверками и не объявляются исправленными.

`FileVorbisBackend` — ограниченный research adapter: отклоняет callback input без
`OggS` до вызова libvorbis и выбирает file entry points для `.ogg`. Он устраняет
неудачные Vorbis probes для WAV в данном fixture profile. Он не чинит malformed OGG
и не является полноценной format validation или production decoder.
Исправленный профиль использует исходный native vtable без этого adapter;
adapter сохранён только для воспроизведения прежних результатов.

## Оставшиеся ограничения

`ProductionEligible` в итоговом manifest всегда false: это изолированная проверка
зависимости, а App/DI/ECS аудиомодуль ещё не реализован. Native fix снимает
failed-init leak blocker на Windows x64; он не объявляет готовым весь аудиомодуль.

В bundled libvorbis отсутствует usable length; ресурсный streaming API возвращает
`not_implemented`. На loop cursor неизвестной длины остаётся монотонным. Поэтому
OGG loop проверяется продолжением ненулевого PCM после известного fixture EOF;
длительность production asset следует получать отдельно. Точный modulo loop
проверяется на WAV с известным числом frames.

Process-only WASAPI loopback пробуется только для audible mode. Его ошибка сохраняется
в `LoopbackUnavailableOutcome`; захват чужого system audio или микрофона не включается.
При недоступном loopback слышимый результат требует подтверждения слушателя.
`*.rendered.wav` содержит PCM микшера до device gain, а не запись колонок/OS output.

Native allocator наблюдает только allocations через заданные callbacks. Внутренние
malloc libvorbis и OS allocations не объявляются подсчитанными; process trends —
дополнительное bounded evidence. Reverse P/Invoke attaches workers к CLR; post-GC
handles отличаются от немедленного native drain. Diagnostic callback и allocator
содержат managed instrumentation, поэтому timing не является production RT benchmark.
Device-loss/default-device switching, AOT/trimming, другие OS/RIDs и длительный soak
не проверяются этим коротким прототипом.

## Как читать результаты

`Completed=true` означает завершение выбранного профиля. В legacy-режиме это
routed fixtures и воспроизведение vendor defects; в patched-режиме — native
RED/GREEN control и consumer без guard. Это не production readiness аудиомодуля.
Смотреть отдельно `Reports`, `KnownDefects`, `DeviceRequested`, `MutedDevice`,
`ProductionEligible`, callback observations и confirmed audible scope.
Не выдавать `device-muted` или rendered PCM за подтверждённую слышимость.

Датированные результаты и ограничения сохраняются также в engine STATUS/HISTORY;
ignored TestResults не является единственным свидетельством проверки.
