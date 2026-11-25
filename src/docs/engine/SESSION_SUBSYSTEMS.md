# Подсистемы игровой сессии — ARCH1j

Общий слой подключения к Aether Runtime, 2026-11-15. Kton и Kul остаются
самостоятельными модулями; этот слой не зависит от них и не активирует backend.
[План](PLAN.md#arch1j-session-subsystems), [текущие проверки](STATUS.md),
[независимый пример](../../ManualTests/RuntimeConsumer/SessionSubsystemProof.cs).

## Подключение и границы API

- `TrueMoon.Aether.Sessions.IGamePlaybackSession` — необязательное
  уведомление о фактическом Play/Pause. Формат IPC не меняется.
- `TrueMoon.Aether.Ecs.Motion.ExternalMotion` — reference-free marker:
  встроенный Movement не интегрирует velocity такой сущности. Подсистема управляет
  marker на structural boundaries и записывает позу до Transform/Extraction.
- `TrueMoon.Aether.Runtime.Ecs.Runtime.ISceneSubsystem` — владелец подготовленных
  ресурсов, список `EcsSystemRegistration` и `SetPlaying(bool)`.
- `SceneSubsystemArguments` — borrowed private `SceneWorld`, точный `FixedStep`
  сессии и cancellation token подготовки. Контейнер подсистеме не передаётся.
- `SceneSubsystemSession` — caller-owned результат существующего
  `IFactory<SceneSubsystemSession, SceneSubsystemArguments>`; объединяет владельцев,
  копирует регистрации и удерживает World lease до успешной очистки.

В composition root зарегистрировать `IFactory<TSubsystem, SceneSubsystemArguments>`
через DI, затем один раз вызвать
`UseAetherSceneSubsystems(builder => builder.Add<TSubsystem>("module.id"))`.
Builder копируется во время конфигурации; поздние изменения не меняют выбор.
Фабрики разрешаются при активации общей фабрики, а не при tick/entity.
До создания сцены subsystem instances, системы и native resources не создаются.
Оба порядка регистрации общего слоя и `UseAetherPrimitiveEcs` поддержаны.
Повторное подключение общего слоя, повторные IDs и aliases owned instances запрещены.

Регистрация без подсистем не обязательна. Runtime без неё сохраняет прежний путь.
Существующий primitive game/session profile автоматически использует общий слой,
если он зарегистрирован. Самостоятельная сессия может получить ту же фабрику,
объединить её `Systems` с собственным графом и соблюдать тот же порядок cleanup.
Ручной low-level consumer обязан передавать этой сессии actual playback notifications.

Лимиты v1: не более64подсистем, непустой ID до128символов, до1024регистраций
подсистем. Общий scheduler по-прежнему ограничен1024системами, включая builtins.
Metadata/order/access graph проверяются существующим scheduler; второй scheduler,
DI scope, фоновый simulation timer и новый factory API не вводятся.

## Подготовка, выполнение и владение

1. Loader материализует private scene. Общая фабрика получает exclusive subsystem
   lease; повторная подготовка или подготовка при уже активном scheduler запрещена.
2. DI-фабрики по порядку создают независимые owners для этой сцены. Подготовка
   синхронна и cooperative; file IO/decode/native preparation выполняются здесь,
   вне simulation tick и audio callback. Фабрика возвращает paused/silent owner.
3. Регистрации копируются, owners получают `SetPlaying(false)`. Общий scheduler
   создаёт системы через фабрики; resource owner и owned system — разные объекты.
4. Primitive profile добавляет Movement → Transform → Extraction и дополнительные
   системы по их phase/access/order metadata. Начальный frame имеет tick0;
   `SceneWorldGameSession` публикует сцену только после полной подготовки.
5. Вызовы tick и playback сериализуются владельцем. Неудачный tick не публикует
   новый frame; подсистемы приостанавливаются. Ошибка playback fault-ит World,
   выполняется best-effort pause остальных owners; resume запрещён.
6. Очистка: Stop/Dispose систем scheduler → pause/Dispose подсистем в обратном
   порядке → scene/World → borrowed App dependencies. App не принимает владение
   factory results; dispose сессий выполняет их потребитель.

Подсистема освобождает только свои resources. Системы не освобождают borrowed
resource owner; owner не освобождает scheduler-owned system повторно. Borrowed
scene/World, factories и App services не освобождаются подсистемой.
Если фабрика не смогла создать owner, она сама очищает частичный результат либо
бросает `SceneWorldPreparationException(error, cleanupOwner)`. Общий слой сохраняет
этот owner, исходную ошибку и earlier resources при повторной ошибке cleanup.
После любой неудачной подготовки candidate требуется отбросить.

Failed cleanup сохраняет World lease и зависимости для явного retry Dispose;
успешные releases не повторяются. Earlier owners не освобождаются после ошибки
более позднего owner. Во время retained cleanup освобождение scene/World запрещено.
Cleanup не отменяется preparation token. Reentrant callbacks/Dispose запрещены.

## Фактический Play/Pause

Сессия начинает работу в Paused. RuntimeProtocol передаёт необязательному
`IGamePlaybackSession` изменения фактического `_playing`: SetMode, Stop,
потерю mesh-view readiness/отсоединение surface и возобновление после актуального
presentation fence. Dispose протокола также приостанавливает playback, но не
освобождает borrowed game. Failed pause оставляет протокол доступным для cleanup
retry, запрещая ticks/resume. Повторные неизменившиеся значения не дают лишних
callbacks; собственные subsystem callbacks обязаны быть идемпотентными.

Paused Step выполняет один simulation tick без включения непрерывного playback.
Physics time продвигается только tick-ом; real-time звук должен явно соблюдать
paused mode. Музыка/звуки не должны продолжаться только потому, что tick больше
не вызывается. Конкретная pause/voice policy принадлежит audio adapter.

## Следующие адаптеры

Общий ARCH1j и [Kton PHYS5](../physics/ECS_BRIDGE.md) завершены2026-11-15.
[Kul ARCH1h.2a](../audio/ECS_BRIDGE.md) также завершён как optional nonspatial adapter.
Kton выбирает component schemas, body reconciliation, exact fixed step,
writeback/ExternalMotion и next-tick contact events. Kul выбирает nonspatial
sound/music profile, подготовку immutable captured inputs и playback/session owners.
Подключение редакторского preview и игрового Runtime должно явно выбирать аудио
в composition root, чтобы оба процесса не воспроизводили одну сцену одновременно.

Общий слой сам по себе не заменяет отдельную приёмку concrete adapters, spatial audio, device loss/recovery,
Nom bridge, checkpoint capture/restore subsystem state или native-state migration
при mod reload. Эти capabilities требуют собственных контрактов и приёмки.
Функциональные import/ED/Player очереди и physical/release gates сохраняются.
