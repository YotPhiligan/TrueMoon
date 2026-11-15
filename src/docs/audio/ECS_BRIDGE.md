# Kul в игровой сессии Aether

Первый nonspatial sound/music profile — **ARCH1h.2a**,2026-11-15.
Единственная детальная задача — [engine PLAN](../engine/PLAN.md#arch1h2a-kul-session).
Базовый [контракт сессии](../engine/SESSION_SUBSYSTEMS.md) и самостоятельный
[Kul App API](README.md) сохраняют своих владельцев и lifecycle.

## Подключение

`TrueMoon.Aether.Kul` содержит reference-free компоненты, schema и чистый manifest
codec. Он зависит только от Aether.Abstractions, без Runtime, Kul/native, Tooling/UI.
`TrueMoon.Aether.Kul.Runtime` — optional adapter Runtime+Kul. Ни Editor preview, ни
базовый Runtime не включают аудио автоматически. Composition root явно выбирает:

```csharp
context.UseAudio(options => options.Mode = AudioMode.Device);
context.UseAetherEcs().UseAetherPrimitiveEcs();
context.UseAetherSceneLoading(_ => AudioComponentRegistry.Register(
    PhysicsComponentRegistry.Register(PrimitiveEcsComponents.Register(new()))).Build());
context.UseBepuPhysics();
context.UseAetherKton();
context.UseAetherKul(new AudioContentReference(absoluteCaptureDirectory, manifestSha256));
context.UseAetherSceneSubsystems(subsystems => subsystems.AddKtonPhysics().AddKulAudio());
```

Для одного Kul физические строки/registry не нужны. Сначала регистрируется provider,
затем bridge. `UseAetherSceneSubsystems` вызывается один раз для всех подсистем.
Обычный `PrimitiveGameSession`/`PrimitiveSimulation` получает их через общие DI factories.
Повторная регистрация bridge отклоняется. Build/Start не создают scene sessions,
не открывают capture; native device принадлежит самостоятельному UseAudio/App.
Сервисы/системы создаются factories, resolution не происходит внутри tick.

`UseConfiguredAetherKul(reference, overrides)` использует точный
`AudioBridgeConfigurationSchema.Default`, добавленный в общий configuration profile:

| Ключ `Audio:Bridge:*` | Default | Граница |
| --- | --- | --- |
| MaximumSources |64|1..256attached sources/scene |
| MaximumPreparedVoices |64|1..256fixed voices/scene |
| MaximumAssets |32|1..64captured files |
| MaximumEncodedBytes |268435456|1..1073741824encoded bytes |

Options копируются при подключении. `Audio:*` App native limits применяются отдельно,
включая одновременно старую, подготовляемую и retiring sessions. Недостаток capacity
отклоняет кандидата; voice stealing/молчаливой обрезки нет. Source gain0..1, master
gain задаётся общим Kul API; float mixer не добавляет limiter.

## Captured audio content v1

Runtime получает **готовую неизменяемую директорию**, названную SHA256 сырых байтов
`audio-content.json`. `AudioContentReference` хранит абсолютный путь и этот hash.
Никакого поиска project/cwd/исходного файла при отсутствии capture нет.

Tooling/consumer явно готовит `AudioContentManifest` из `AudioContentAsset`:
stable `AudioAssetId`, flat portable WAV/OGG name, SHA256/encoded length,
`Sound` или `Music`, фиксированное число `Voices`1..16. Codec v1 ограничивает metadata
64KiB и64assets; identities/file names уникальны, file names сравниваются без учёта
регистра; traversal/absolute/reserved Windows names, unknown/duplicate JSON fields,
неподдерживаемая версия, неверные hashes/length/kind/capacity отклоняются.
`Encode` детерминированно сортирует IDs. В директории только manifest и declared files.

Default `AudioContentLease.Open` проверяет **весь** список/размеры/hashes до создания
native AudioSession и удерживает read-only handles с FileShare.Read. Обычные Windows
write/delete заблокированы до завершения native stream cleanup. Reparse root/files
и дополнительные entries отклоняются. Это trusted immutable producer contract,
не sandbox для враждебной ОС/гонок изменения родительских директорий.

`IFactory<IAudioContentLease, AudioContentLoadArguments>` можно явно зарегистрировать
до UseAetherKul для другого immutable provider. Его результат — caller-owned lease;
failed creation должна очиститься либо вернуть retained cleanup через
`SceneWorldPreparationException`. Common session удерживает World до успешной очистки.

Capture фиксируется в App composition и доступен его игровым сессиям. Автоматический
Editor import/Nom adapter, включение audio capture в GameBuildArtifact schema/ABI и
автоматическая загрузка этого SDK в collectible game/mod ALC — отдельные этапы.
Существующий explicit shared-assembly механизм mod loader не расширяется скрыто.
Воспроизводимый producer/consumer есть в [ManualTests/KulEcs](../../ManualTests/KulEcs/README.md).

## Компоненты и команды

`AudioComponentRegistry.Register` добавляет optional `AudioComponentSchemas.Source`
v1: `asset` (nonempty GUID D), `volume`, `loop`, `autoPlay`. Asset должен присутствовать
в capture. Transform/listener не нужны: этот профиль не пространственный.
Runtime-only `AudioPlaybackCommand` и `AudioSourceState` не сериализуются в authoring scene.

Один `AudioSource` резервирует один заранее подготовленный голос своего asset.
`AudioEntityCommands` создаёт/добавляет/удаляет **полный** набор source/state/command
на phase boundary; `DeclareWrites` объявляет доступ системы. Добавление аудио не
трогает Transform/Kton components. Динамическая привязка без state и неполное удаление
owned roles отклоняются. EntityHandle с generation защищает повторное использование slot.

| Изменение | Поведение |
| --- | --- |
| Volume/Loop | Меняются на существующем голосе |
| AutoPlay | Только initial attachment/rebinding; дальнейшее управление командами |
| Asset | Старый голос Stop/rewind, новая reservation; новый AutoPlay, прежняя command sequence сохранена |
| Play | Rewind и желаемый Playing, включая EOF |
| Resume | Продолжить с позиции; natural EOF не повторяется |
| Pause | Сохранить позицию, не возобновлять из-за global Play |
| Stop | Остановить и перемотать, reservation остаётся |
| Seek | Sample-frame destination, сохраняет желаемый режим; unknown-length OGG допускает только0 |

Command.Sequence положительна и монотонна для attachment/entity generation.
Повтор той же sequence разрешён только с тем же payload и не исполняется повторно;
уменьшение/изменение старого payload отклоняется. Сохранённый Seek не применяется к
новому asset повторно. За границей известной length seek отклоняется до controls.

## Порядок, пауза и ownership

До публикации scene: validate/pin capture → caller-owned AudioSession → decode shared
Sound clips/prepare fixed sound+music voices → source preflight → initial state.
`StartPaused=true`, `RetainAtEnd=true` гарантируют silent preparation и сохранение
пула после EOF, даже когда другой App consumer запускает новый звук. Defaults этих
additive Kul options остаются false; прежние consumers сохраняют auto-start/EOF collection.
`AudioPlayback.Loop` теперь доступен для изменения уже подготовленного голоса.

`kul.audio` работает в Extraction после `builtin.primitive-extraction`.
Gameplay команды применяются до него; Kton contacts предыдущего tick можно превратить
в audio command в Gameplay того же tick. Сначала проверяется весь candidate set,
потом native controls и запись State. В tick нет resolution, открытия/decoding файлов
или выделения новых native voices. Streaming workers продолжают обычное чтение;
seek/refill может временно дать silence, RT/deadline SLA не вводится.

Фактический `SetPlaying(false)` при Pause/fence/Stop останавливает продвижение voices,
сохраняя per-source намерение. `SetPlaying(true)` продолжает только нужные sources.
Paused Step принимает команды и меняет ECS, сохраняя silence; Play начнётся после
реального перехода session в Playing. `AudioSourceState` — sample конца tick,
его cursor не обещает realtime обновление между ticks. `TryGetState` — текущая
диагностика только для живой привязки; device time и simulation time независимы.

Native side effects не откатываются. Ошибка controls/tick терминальна, bridge и
общий controller best-effort pause voices; предыдущий полный render snapshot сохранён.
Неудачный новый кандидат очищается отдельно, старая опубликованная session продолжает
работу. При Replace новый owner готовится paused; старый освобождается по общему протоколу.

Cleanup: scheduler Stop/Dispose → AudioSession voices/clips/stream drain → capture
pins → SceneWorld/World. При ошибке освобождения owner/World lease удерживаются для
Dispose retry. App native fallback cleanup не означает освобождение caller-owned
World/content; поздний Dispose bridge завершает его собственное владение.

Windows x64 first profile. Spatial/listener/occlusion, device-loss recovery,
hard peak/RT/async queues, long soak, другие OS и native-state hot migration остаются
отдельными AUD2–3/engine задачами. Offline PCM и muted actual WASAPI — разные proofs;
физическое прослушивание не выводится из них автоматически.
