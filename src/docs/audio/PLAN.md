# Kul / Audio: план модуля

<a id="current-audio-queue"></a>
## Текущая очередь — 2026-11-12

Пользователь запросил самостоятельный аудиомодуль после исправления native leaks.
Позднее2026-11-12 выбрано и применено имя **TrueMoon.Kul**: [решение](NAMING.md),
[новая приёмка](HISTORY.md#kul-rename-2026-11-12), [manifest](KUL_RENAME_RESULTS.json).
Функциональные имена Audio API и ключи Audio:* сохранены.
Направление выделено из [ARCH1h](../engine/PLAN.md#arch1h-audio). Здесь — единственный
детальный план модуля; ECS/session bridge движка остаётся отдельным потребителем.

1. **AUD1 Windows v1** — App/DI, звуки, музыка, native ownership, common configuration,
   source/published/fresh-package acceptance. **Завершён в указанном Windows x64 scope**;
   [фактические проверки](HISTORY.md#aud1-2026-11-12), [manifest](AUD1_RESULTS.json).
2. **AUD2** — device-loss/recovery и долгий real-time workload; bounded async preparation,
   command admission/backpressure и hard peak budgets по требованиям потребителей.
3. **AUD3** — Linux/macOS native build/ABI/device matrix, затем optional spatial audio.

## Принятые границы AUD1

Один пакет `TrueMoon.Kul`, без Aether/UI/rendering. Module-specific API остаётся в нём,
общие App/configuration/factory contracts — в TrueMoon.Contracts. Backend — exact local
`TrueMoon.MiniAudioEx 4.0.1-truemoon.1`, Windows x64, source-built libvorbis patch.
Пакеты Abstractions/provider пока не выделяются без независимых consumers.

`UseAudio` или `UseConfiguredAudio` в App.Setup валидирует и копирует options.
Build не открывает устройство. Один DI-owned AudioHost создаёт mixer/context/resource
manager при Start; IAudioService и factory/lifecycle adapters не владеют вторым Dispose.
Один lifecycle adapter implements Start/Stop для failed-start rollback и App deduplication.
Повторное подключение отклоняется. Стандартный контейнер — Cobalt; Microsoft DI adapter
проверяется отдельно. Опции используют общий Audio schema/profile, без OS/JSON readers.

`IFactory<AudioSession>` создаёт caller-owned session. App отслеживает её как fallback;
ранний Dispose удаляет её из App. Сессия владеет клипами и voices. Clip — загруженный
decoded sound, voice — отдельный cursor/gain/loop. Выпущенный clip больше не играет
новые voices, существующие удерживают общий PCM до последнего Dispose. Music voice
стримит файл на native workers. Pause/Stop держат resources, Dispose освобождает;
EOF voices собираются явно либо при следующем admission. Между sessions clips не передаются.

Порядок teardown: закрыть admission → native device stop/drain → voices → clips → engine/
device → resource manager/worker join → decoder-table storage → context.
SafeHandle children удерживают parent handles, включая finalization. Ошибка drain удерживает
граф для retry; cleanup не отменяется CancellationToken. Сам App после Stop не рестартуется.
Native callback не вызывает managed код и не берёт managed control lock. Загрузка/подготовка
файла синхронна вне игрового/audio callback; runtime commands сериализованы в App control lock.
Stream seek запрашивает worker refill и может временно выдавать silence.

## Admission и ограничения

По умолчанию 8 sessions /64 clips /64 voices, из них до2 music streams; stereo float32,
48kHz, requested256frame device period; начальный master gain0.5. Volume0..1, finite.
Encoded SFX≤16MiB перед decoding, retained decoded SFX≤128MiB после preparation; один
shared clip учитывается один раз, включая released clip с живыми voices. Последняя
проверка не ограничивает transient decoder peak. Страницы streaming используют native
defaults; hard global stream-page budget и RT SLA не заявляются.
Float32 mix не нормализуется и не имеет нашего limiter: сумма voices может превышать
unity. Потребитель выбирает gain/headroom; clipping/limiter policy для расширенного
workload принимается в AUD2.

Device — actual WASAPI или явная startup error, offline mode выбран явно и не открывает
устройство. Файлы локальные, неизменные до освобождения; captured asset identity/Nom
bridge пока не реализованы. OGG duration может быть unavailable, loop cursor — monotonic.
Нет автоматического device-loss recovery, пространственного звука и ECS integration.

## Критерии приёмки AUD1

- Известные PCM/gain/stereo, independent voices, pause/resume/seek/stop/EOF/loop.
- OGG streaming и длинный WAV без whole-file PCM; malformed/truncated/bad-CRC отказ
  не теряет capacity/handles/files; корректный OGG играет после отказов.
- App start/rollback/Stop/Dispose, ранний session/clip/voice Dispose, aliases, лимиты,
  options snapshot/configuration и два App; concurrent control/cleanup serialized.
- Direct module suite Debug/Release и discovery через solution; native startup checkpoints.
- Source Debug/published Release/fresh package-only consumer с exact DLL/native SHA;
  actual WASAPI приглушён, audibility не подменяется offline PCM.
- Воспроизводимая сборка fork, pinned sources/licenses/toolchain/package feed и инструкция
  нового checkout. Новые результаты отдельно от исторического native-fix prototype.
